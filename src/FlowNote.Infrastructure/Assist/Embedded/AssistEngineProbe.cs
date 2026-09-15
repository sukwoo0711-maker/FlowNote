using System.Text.Json;
using FlowNote.Core.Assist;

namespace FlowNote.Infrastructure.Assist.Embedded;

public static class AssistEngineProbe
{
    public static async Task<int> RunAsync(
        EmbeddedEngineManager manager,
        IContextInference inference,
        string outputPath,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var check = manager.LastCheck;
        if (!check.Ready)
        {
            await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new
            {
                layer = "ENGINE_REAL",
                status = "BLOCKED",
                reason = check.Reason ?? "needs-assets"
            }, Indented()), cancellationToken);
            return 2;
        }

        await manager.EnsureReadyAsync(cancellationToken);
        var request = new InferenceRequest
        {
            EntryId = "probe-1",
            NoteText = "인버터 전원을 켜면 과전류가 발생한다.",
            Candidates = [],
            EntryRevision = "probe",
            CorrectionRevision = 0,
            PolicyRevision = 1
        };
        var inferred = await inference.InferAsync(request, cancellationToken);

        var port = manager.CurrentPort;
        var pidOwned = OperatingSystem.IsWindows()
                       && TcpPortOwner.TryGetListenerPid(port, out var pid)
                       && pid == manager.CurrentProcessId;
        var authFailedWithoutKey = false;
        if (port > 0)
        {
            using var anon = new LocalLlamaHttpClient(new Uri($"http://127.0.0.1:{port}/"));
            try
            {
                await anon.ChatCompletionsAsync(new { model = manager.CurrentAlias, messages = Array.Empty<object>() }, cancellationToken);
            }
            catch (InvalidOperationException ex) when (ex.Message == "engine-auth-failed")
            {
                authFailedWithoutKey = true;
            }
        }

        var apiOk = inferred.ErrorCode is not "engine-auth-failed"
                    and not "engine-host-not-loopback"
                    and not "model-unavailable-retry"
                    and not "engine-exited"
                    and not "engine-pid-mismatch";
        var pass = (manager.State is EmbeddedEngineState.Ready or EmbeddedEngineState.Busy)
                   && port is > 0 and not 11434 and not 11435
                   && pidOwned
                   && authFailedWithoutKey
                   && apiOk;

        var payload = new
        {
            layer = "ENGINE_REAL",
            status = pass ? "PASS" : "FAIL",
            provider = AssistVersions.EmbeddedProvider,
            releaseTag = AssistVersions.EngineReleaseTag,
            backend = "cpu",
            alias = manager.CurrentAlias,
            port,
            pidOwned,
            authFailedWithoutKey,
            responseFormat = LlamaChatRequest.ActiveFormat,
            arguments = manager.LastArguments,
            fingerprint = check.Fingerprint,
            probeDecision = AssistCodec.Decision(inferred.Decision),
            probeError = inferred.ErrorCode,
            timestamp_utc = DateTimeOffset.UtcNow.ToString("o")
        };
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(payload, Indented()), cancellationToken);
        return pass ? 0 : 1;
    }

    private static JsonSerializerOptions Indented()
        => new() { WriteIndented = true };
}
