using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FlowNote.Core.Assist;

namespace FlowNote.Infrastructure.Assist.Embedded;

public sealed class LocalLlamaHttpClient : IDisposable
{
    private readonly HttpClient _http;

    public LocalLlamaHttpClient(Uri baseAddress)
    {
        if (!IPAddress.TryParse(baseAddress.Host, out var address) || !IPAddress.Loopback.Equals(address))
        {
            throw new InvalidOperationException("engine-host-not-loopback");
        }

        var port = baseAddress.Port;
        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(AssistVersions.ConnectTimeoutSeconds),
            ConnectCallback = async (context, cancellationToken) =>
            {
                if (!IPAddress.TryParse(context.DnsEndPoint.Host, out var connectAddress) ||
                    !IPAddress.Loopback.Equals(connectAddress) ||
                    context.DnsEndPoint.Port != port)
                {
                    throw new InvalidOperationException("engine-host-not-loopback");
                }

                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(IPAddress.Loopback, port, cancellationToken).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };
        _http = new HttpClient(handler)
        {
            BaseAddress = baseAddress,
            Timeout = Timeout.InfiniteTimeSpan
        };
        _http.DefaultRequestHeaders.ExpectContinue = false;
    }

    public void SetApiKey(string key)
    {
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
    }

    public async Task<bool> TryHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(AssistVersions.ConnectTimeoutSeconds));
            using var response = await _http.GetAsync("/health", cts.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<string?> TryModelsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            using var response = await _http.GetAsync("/v1/models", cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await ReadLimitedStringAsync(response, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<string> ChatCompletionsAsync(object payload, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(AssistVersions.HttpTimeoutSeconds));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
        if ((int)response.StatusCode is 401 or 403)
        {
            throw new InvalidOperationException("engine-auth-failed");
        }

        if ((int)response.StatusCode is 503 or 429)
        {
            throw new InvalidOperationException("model-unavailable-retry");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("model-http-" + (int)response.StatusCode);
        }

        return await ReadLimitedStringAsync(response, cts.Token).ConfigureAwait(false);
    }

    public void Dispose() => _http.Dispose();

    private static async Task<string> ReadLimitedStringAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var block = new byte[4096];
        var total = 0;
        int read;
        while ((read = await stream.ReadAsync(block.AsMemory(0, block.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > AssistVersions.MaxResponseBytes)
            {
                throw new InvalidOperationException("response-too-large");
            }

            buffer.Write(block, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
