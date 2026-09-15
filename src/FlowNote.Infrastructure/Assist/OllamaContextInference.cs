using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FlowNote.Core.Assist;

namespace FlowNote.Infrastructure.Assist;

public sealed class OllamaContextInference : IContextInference, IDisposable
{
    private readonly HttpClient _http;
    private readonly string _model;
    private DateTimeOffset _availableUntil;
    private bool? _available;

    public OllamaContextInference(string baseUrl, string modelTag)
    {
        var uri = new Uri(baseUrl, UriKind.Absolute);
        if (!IPAddress.TryParse(uri.Host, out var address) || !IPAddress.IsLoopback(address))
        {
            throw new InvalidOperationException("ollama-host-not-loopback");
        }

        _model = modelTag;
        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(2)
        };
        _http = new HttpClient(handler)
        {
            BaseAddress = uri,
            Timeout = TimeSpan.FromSeconds(AssistVersions.HttpTimeoutSeconds)
        };
        _http.DefaultRequestHeaders.ExpectContinue = false;
    }

    public bool IsFake => false;

    public bool IsAvailable
    {
        get
        {
            if (_available is not null && DateTimeOffset.UtcNow < _availableUntil)
            {
                return _available.Value;
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                using var response = _http.GetAsync("/api/tags", cts.Token).GetAwaiter().GetResult();
                _available = response.IsSuccessStatusCode;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _available = false;
            }

            _availableUntil = DateTimeOffset.UtcNow.AddSeconds(10);
            return _available.Value;
        }
    }

    public async Task<InferenceResult> InferAsync(InferenceRequest request, CancellationToken cancellationToken)
    {
        var candidates = CandidateSelector.FitBudget(request.NoteText, request.Candidates, AssistVersions.MaxInputUtf8Bytes);
        var user = new
        {
            entry_id = request.EntryId,
            note_text = request.NoteText,
            attachment_names = request.AttachmentNames,
            candidates = candidates.Select(item => new
            {
                thread_id = item.ThreadId,
                title = item.Title,
                excerpts = item.Excerpts,
                issue_key = item.IssueKey
            })
        };
        using var schema = JsonDocument.Parse(PromptCatalog.ResponseSchemaJson);
        var format = schema.RootElement.Clone();
        var counted = JsonSerializer.Serialize(new
        {
            messages = new object[]
            {
                new { role = "system", content = PromptCatalog.SystemPrompt },
                new { role = "user", content = JsonSerializer.Serialize(user) }
            },
            format
        });
        if (Encoding.UTF8.GetByteCount(counted) > AssistVersions.MaxInputUtf8Bytes)
        {
            return RulesEngine.Abstain(request.EntryId, "analysis-too-long");
        }

        var payload = new
        {
            model = _model,
            messages = new object[]
            {
                new { role = "system", content = PromptCatalog.SystemPrompt },
                new { role = "user", content = JsonSerializer.Serialize(user) }
            },
            format,
            stream = false,
            think = false,
            keep_alive = "60s",
            options = new { temperature = 0, seed = 42, num_ctx = 8192, num_predict = 768 }
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(payload)
        };
        using var response = await _http.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if ((int)response.StatusCode is 503 or 429)
        {
            return RulesEngine.Abstain(request.EntryId, "model-unavailable-retry");
        }

        if (!response.IsSuccessStatusCode)
        {
            return RulesEngine.Abstain(request.EntryId, "model-http-" + (int)response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var block = new byte[4096];
        var total = 0;
        int read;
        while ((read = await stream.ReadAsync(block.AsMemory(0, block.Length), cancellationToken)) > 0)
        {
            total += read;
            if (total > AssistVersions.MaxResponseBytes)
            {
                return RulesEngine.Abstain(request.EntryId, "response-too-large");
            }

            buffer.Write(block, 0, read);
        }

        var text = Encoding.UTF8.GetString(buffer.ToArray());
        using var document = StrictJson.Parse(text);
        if (!document.RootElement.TryGetProperty("done", out var done) || done.ValueKind != JsonValueKind.True)
        {
            return RulesEngine.Abstain(request.EntryId, "model-not-done");
        }

        if (document.RootElement.TryGetProperty("message", out var message) &&
            message.TryGetProperty("tool_calls", out _))
        {
            return RulesEngine.Abstain(request.EntryId, "tool-calls-forbidden");
        }

        if (!document.RootElement.TryGetProperty("message", out message) ||
            !message.TryGetProperty("content", out var content) ||
            content.ValueKind != JsonValueKind.String)
        {
            return RulesEngine.Abstain(request.EntryId, "missing-content");
        }

        var inner = content.GetString() ?? "";
        try
        {
            StrictJson.EnsureNoDuplicateProperties(inner);
            using var parsed = JsonDocument.Parse(inner);
            return Map(parsed.RootElement, request);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return RulesEngine.Abstain(request.EntryId, "invalid-json");
        }
    }

    public void Dispose() => _http.Dispose();

    private static InferenceResult Map(JsonElement root, InferenceRequest request)
        => InferenceResultMapper.Map(root, request, isFake: false);
}
