using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using FlowNote.Core.Assist;
using FlowNote.Infrastructure.Paths;

namespace FlowNote.Infrastructure.Assist.Embedded;

public enum EmbeddedEngineState
{
    Disabled,
    NeedsAssets,
    Stopped,
    Starting,
    Ready,
    Busy,
    Stopping,
    Faulted
}

public sealed class EmbeddedEngineManager : IDisposable
{
    private readonly AssetVerifier _verifier;
    private readonly AppStoragePaths _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _infer = new(1, 1);
    private NativeJobProcess? _child;
    private LocalLlamaHttpClient? _http;
    private string? _key;
    private string? _alias;
    private string? _keyPath;
    private int _port;
    private int _processId;
    private long _generation;
    private long _idleGeneration;
    private int _inFlight;
    private DateTimeOffset _lastUsedUtc = DateTimeOffset.MinValue;
    private bool _disposed;
    private bool _disabled;
    private IReadOnlyList<string>? _lastArguments;

    public EmbeddedEngineManager(AssetVerifier verifier, AppStoragePaths paths)
    {
        _verifier = verifier;
        _paths = paths;
        State = EmbeddedEngineState.Stopped;
    }

    public string? CurrentAlias { get; private set; }

    public int CurrentPort => _port;

    public int CurrentProcessId => _processId;

    public IReadOnlyList<string>? LastArguments => _lastArguments;

    public EmbeddedEngineState State { get; private set; }

    public string StatusText => State switch
    {
        EmbeddedEngineState.NeedsAssets => "모델 파일 필요 · AI 없이 계속 가능",
        EmbeddedEngineState.Starting => "로컬 엔진 준비 중",
        EmbeddedEngineState.Ready => "로컬 엔진 준비됨",
        EmbeddedEngineState.Busy => "로컬 분석 중",
        EmbeddedEngineState.Faulted => "로컬 엔진 오류 · 다시 시도",
        EmbeddedEngineState.Disabled => "로컬 분석 끔",
        _ => "로컬 엔진 대기"
    };

    public AssetCheck LastCheck => _verifier.Check();

    public async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_disabled || State == EmbeddedEngineState.Disabled)
            {
                throw new InvalidOperationException("assist-off");
            }

            if (State is EmbeddedEngineState.Ready or EmbeddedEngineState.Busy)
            {
                return;
            }

            var assets = _verifier.Check();
            if (!assets.Ready)
            {
                State = EmbeddedEngineState.NeedsAssets;
                throw new InvalidOperationException(assets.Reason ?? "needs-assets");
            }

            State = EmbeddedEngineState.Starting;
            Exception? last = null;
            for (var attempt = 0; attempt < AssistVersions.PortAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await StartOnceAsync(assets, cancellationToken);
                    State = EmbeddedEngineState.Ready;
                    return;
                }
                catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException)
                {
                    last = ex;
                    await CleanupChildAsync();
                }
            }

            State = EmbeddedEngineState.Faulted;
            throw last ?? new InvalidOperationException("engine-start-failed");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> ChatAsync(IDictionary<string, object?> payload, CancellationToken cancellationToken)
    {
        await _infer.WaitAsync(cancellationToken);
        try
        {
            await EnsureReadyAsync(cancellationToken);
            payload["model"] = CurrentAlias;
            Interlocked.Increment(ref _inFlight);
            var startedGeneration = Interlocked.Read(ref _generation);
            try
            {
                if (_http is null || Interlocked.Read(ref _generation) != startedGeneration)
                {
                    throw new InvalidOperationException("engine-generation-changed");
                }

                State = EmbeddedEngineState.Busy;
                return await _http.ChatCompletionsAsync(payload, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
                _lastUsedUtc = DateTimeOffset.UtcNow;
                if (State == EmbeddedEngineState.Busy)
                {
                    State = EmbeddedEngineState.Ready;
                }

                ScheduleIdleStop();
            }
        }
        finally
        {
            _infer.Release();
        }
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync();
        try
        {
            State = EmbeddedEngineState.Stopping;
            Interlocked.Increment(ref _generation);
            await CleanupChildAsync();
            State = _disabled ? EmbeddedEngineState.Disabled : EmbeddedEngineState.Stopped;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Disable()
    {
        _disabled = true;
        _ = StopAsync();
    }

    public Task ResetForRetryAsync()
    {
        _disabled = false;
        return StopAsync();
    }

    public Task<AssetCheck> ImportModelAsync(string sourcePath, IProgress<double>? progress, CancellationToken cancellationToken)
        => _verifier.ImportModelAsync(sourcePath, progress, cancellationToken);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopAsync().GetAwaiter().GetResult();
        _gate.Dispose();
        _infer.Dispose();
    }

    private async Task StartOnceAsync(AssetCheck assets, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("job-object-windows-only");
        }
        var port = BindEphemeralPort();
        var runId = Guid.NewGuid().ToString("N")[..16];
        var alias = "flownote-local-" + runId;
        var keyBytes = RandomNumberGenerator.GetBytes(32);
        var key = Convert.ToHexString(keyBytes).ToLowerInvariant();
        Directory.CreateDirectory(_paths.EngineSessionDirectory);
        var keyPath = Path.Combine(_paths.EngineSessionDirectory, "key-" + runId + ".txt");
        await File.WriteAllTextAsync(keyPath, key, cancellationToken);
        if (OperatingSystem.IsWindows())
        {
            KeyFileAcl.RestrictToCurrentUser(keyPath);
        }

        var threads = Math.Max(1, Math.Min(4, Environment.ProcessorCount / 2));
        var args = new List<string>
        {
            "--model", assets.ModelPath!,
            "--alias", alias,
            "--host", "127.0.0.1",
            "--port", port.ToString(),
            "--api-key-file", keyPath,
            "--ctx-size", "8192",
            "--parallel", "1",
            "--threads", threads.ToString(),
            "--threads-batch", threads.ToString(),
            "--n-gpu-layers", "0",
            "--jinja",
            "--chat-template-kwargs", "{\"enable_thinking\":false}",
            "--offline",
            "--no-webui",
            "--no-slots",
            "--no-agent",
            "--no-ui-mcp-proxy",
            "--no-context-shift",
            "--cache-ram", "0",
            "--poll", "0"
        };

        if (!OwnedEnginePath.IsAllowed(_verifier.AppBase, assets.EngineExe!))
        {
            throw new InvalidOperationException("engine-path-escape");
        }

        var child = NativeJobProcess.Start(assets.EngineExe!, args, ChildEnvironment());
        _lastArguments = args;
        var http = new LocalLlamaHttpClient(new Uri($"http://127.0.0.1:{port}/"));
        http.SetApiKey(key);
        using var startupCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startupCts.CancelAfter(TimeSpan.FromSeconds(AssistVersions.ColdStartupSeconds));
        try
        {
            await WaitOwnedAsync(child, http, port, alias, startupCts.Token).ConfigureAwait(false);
        }
        catch
        {
            try
            {
                await File.WriteAllTextAsync(
                    Path.Combine(_paths.EngineSessionDirectory, "last-start.log"),
                    child.DiagnosticsSnapshot() + Environment.NewLine + "exited=" + child.HasExited,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
            }

            http.Dispose();
            child.Dispose();
            TryDelete(keyPath);
            throw;
        }

        _child = child;
        _http = http;
        _key = key;
        _alias = alias;
        CurrentAlias = alias;
        _keyPath = keyPath;
        _port = port;
        _processId = child.ProcessId;
        Interlocked.Increment(ref _generation);
        _lastUsedUtc = DateTimeOffset.UtcNow;
    }

    private static async Task WaitOwnedAsync(
        NativeJobProcess child,
        LocalLlamaHttpClient http,
        int port,
        string alias,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (child.HasExited)
            {
                throw new InvalidOperationException("engine-exited");
            }

            try
            {
                if (!await http.TryHealthAsync(cancellationToken))
                {
                    await Task.Delay(400, cancellationToken);
                    continue;
                }

                if (OperatingSystem.IsWindows() &&
                    TcpPortOwner.TryGetListenerPid(port, out var pid) &&
                    pid != child.ProcessId)
                {
                    throw new InvalidOperationException("engine-pid-mismatch");
                }

                if (OperatingSystem.IsWindows() && !TcpPortOwner.TryGetListenerPid(port, out _))
                {
                    await Task.Delay(400, cancellationToken);
                    continue;
                }

                var models = await http.TryModelsAsync(cancellationToken);
                if (models is null || !models.Contains(alias, StringComparison.Ordinal))
                {
                    await Task.Delay(400, cancellationToken);
                    continue;
                }

                return;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(400, cancellationToken);
            }
            catch (HttpRequestException)
            {
                await Task.Delay(400, cancellationToken);
            }
            catch (InvalidOperationException ex) when (
                ex.Message is not "engine-pid-mismatch" and not "engine-exited"
                && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(400, cancellationToken);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task CleanupChildAsync()
    {
        _http?.Dispose();
        _http = null;
        if (_child is not null)
        {
            _child.Terminate();
            _child.WaitForExit(TimeSpan.FromSeconds(AssistVersions.StopDrainSeconds));
            _child.Dispose();
            _child = null;
        }

        if (_keyPath is not null)
        {
            TryDelete(_keyPath);
            _keyPath = null;
        }

        _key = null;
        _alias = null;
        CurrentAlias = null;
        _port = 0;
        _processId = 0;
        await Task.CompletedTask;
    }

    private void ScheduleIdleStop()
    {
        var idleId = Interlocked.Increment(ref _idleGeneration);
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(AssistVersions.IdleStopSeconds));
                if (Interlocked.Read(ref _idleGeneration) != idleId)
                {
                    return;
                }

                if (Volatile.Read(ref _inFlight) != 0)
                {
                    return;
                }

                if (DateTimeOffset.UtcNow - _lastUsedUtc < TimeSpan.FromSeconds(AssistVersions.IdleStopSeconds))
                {
                    return;
                }

                await StopAsync();
            }
            catch (ObjectDisposedException)
            {
            }
        });
    }

    private static int BindEphemeralPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static readonly HashSet<string> ChildEnvironmentAllow = new(StringComparer.OrdinalIgnoreCase)
    {
        "SystemRoot",
        "windir",
        "SystemDrive",
        "PATH",
        "PATHEXT",
        "TEMP",
        "TMP",
        "ComSpec",
        "NUMBER_OF_PROCESSORS",
        "PROCESSOR_ARCHITECTURE",
        "PROCESSOR_ARCHITEW6432"
    };

    private static Dictionary<string, string> ChildEnvironment()
    {
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var key = entry.Key?.ToString();
            if (string.IsNullOrWhiteSpace(key) || !ChildEnvironmentAllow.Contains(key))
            {
                continue;
            }

            env[key] = entry.Value?.ToString() ?? "";
        }

        return env;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}
