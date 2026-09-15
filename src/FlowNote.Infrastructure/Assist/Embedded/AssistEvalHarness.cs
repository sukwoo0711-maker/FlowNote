using System.Text.Json;
using FlowNote.Core.Assist;

namespace FlowNote.Infrastructure.Assist.Embedded;

public static class AssistEvalHarness
{
    public static async Task<int> RunAsync(
        IContextInference inference,
        string fixtureDirectory,
        string outputPath,
        int repeats,
        CancellationToken cancellationToken)
    {
        var cases = LoadCases(fixtureDirectory);
        if (cases.Count == 0)
        {
            await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new
            {
                layer = "MODEL_REAL",
                status = "BLOCKED",
                reason = "eval fixtures missing",
                cases_run = 0
            }), cancellationToken);
            return 2;
        }

        var runs = new List<object>();
        var schemaPass = 0;
        var determinate = 0;
        var determinateCorrect = 0;
        var criticalMiss = 0;
        var forcedLinkOnAbstain = 0;
        var expectedNonAbstain = 0;
        var producedNonAbstain = 0;
        var total = 0;

        for (var round = 1; round <= repeats; round++)
        {
            foreach (var item in cases)
            {
                cancellationToken.ThrowIfCancellationRequested();
                total++;
                var result = await inference.InferAsync(item.Request, cancellationToken);
                var schemaOk = result.ErrorCode is null;
                if (schemaOk)
                {
                    schemaPass++;
                }

                var decision = AssistCodec.Decision(result.Decision);
                var role = result.Primary is null ? null : AssistCodec.Role(result.Primary.Role);
                var threadId = result.Primary?.ThreadId;
                if (item.Determinate)
                {
                    determinate++;
                    var ok = string.Equals(decision, item.ExpectedDecision, StringComparison.Ordinal);
                    if (item.ExpectedRole is not null)
                    {
                        ok = ok && string.Equals(role, item.ExpectedRole, StringComparison.Ordinal);
                    }

                    if (string.Equals(item.ExpectedDecision, "link", StringComparison.Ordinal))
                    {
                        ok = ok && string.Equals(threadId, item.ExpectedThreadId, StringComparison.Ordinal);
                    }

                    if (ok)
                    {
                        determinateCorrect++;
                    }

                    if (item.Critical && !ok && result.Decision != AssistDecision.Abstain)
                    {
                        criticalMiss++;
                    }
                }

                if (string.Equals(item.ExpectedDecision, "abstain", StringComparison.Ordinal) &&
                    result.Decision == AssistDecision.Link)
                {
                    forcedLinkOnAbstain++;
                }

                if (!string.Equals(item.ExpectedDecision, "abstain", StringComparison.Ordinal))
                {
                    expectedNonAbstain++;
                    if (result.Decision != AssistDecision.Abstain)
                    {
                        producedNonAbstain++;
                    }
                }

                runs.Add(new
                {
                    round,
                    entry_id = item.Request.EntryId,
                    schema_ok = schemaOk,
                    error_code = result.ErrorCode,
                    decision,
                    role,
                    thread_id = threadId,
                    expected_decision = item.ExpectedDecision,
                    expected_role = item.ExpectedRole,
                    expected_thread_id = item.ExpectedThreadId,
                    determinate = item.Determinate,
                    critical = item.Critical
                });
            }
        }

        var schemaRate = total == 0 ? 0 : schemaPass / (double)total;
        var accuracy = determinate == 0 ? 0 : determinateCorrect / (double)determinate;
        var coverage = expectedNonAbstain == 0 ? 0 : producedNonAbstain / (double)expectedNonAbstain;
        var allAbstain = expectedNonAbstain > 0 && producedNonAbstain == 0;
        var pass = schemaRate >= 0.95
                   && accuracy >= 0.85
                   && criticalMiss == 0
                   && forcedLinkOnAbstain == 0
                   && !allAbstain;
        var payload = new
        {
            layer = "MODEL_REAL",
            status = pass ? "PASS" : "FAIL",
            model = AssistVersions.ModelTag,
            fingerprint_note = "see engine LastCheck.Fingerprint",
            repeats,
            cases_run = total,
            unique_sentences = cases.Count,
            schema_pass_rate = schemaRate,
            determinate_accuracy = accuracy,
            coverage_non_abstain = coverage,
            critical_miss = criticalMiss,
            forced_link_on_abstain = forcedLinkOnAbstain,
            all_abstain = allAbstain,
            semantic_auto_apply = false,
            response_format = LlamaChatRequest.ActiveFormat,
            timestamp_utc = DateTimeOffset.UtcNow.ToString("o"),
            runs
        };
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(
            outputPath,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        return pass ? 0 : 1;
    }

    private static List<EvalCase> LoadCases(string fixtureDirectory)
    {
        var list = new List<EvalCase>();
        foreach (var name in new[] { "dev", "regression" })
        {
            var inputs = Path.Combine(fixtureDirectory, name + ".inputs.jsonl");
            var expected = Path.Combine(fixtureDirectory, name + ".expected.jsonl");
            if (!File.Exists(inputs) || !File.Exists(expected))
            {
                continue;
            }

            var wanted = File.ReadAllLines(expected)
                .Where(static line => !string.IsNullOrWhiteSpace(line))
                .Select(static line => JsonDocument.Parse(line).RootElement.Clone())
                .ToDictionary(static item => item.GetProperty("entry_id").GetString() ?? "", StringComparer.Ordinal);
            foreach (var line in File.ReadAllLines(inputs))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                var id = root.GetProperty("entry_id").GetString() ?? "";
                if (!wanted.TryGetValue(id, out var exp))
                {
                    continue;
                }

                list.Add(new EvalCase(MapRequest(root), exp));
            }
        }

        return list;
    }

    private static InferenceRequest MapRequest(JsonElement root)
    {
        var candidates = new List<ThreadCandidate>();
        if (root.TryGetProperty("candidates", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in array.EnumerateArray())
            {
                var excerpts = new List<string>();
                if (item.TryGetProperty("excerpts", out var excerptEl) && excerptEl.ValueKind == JsonValueKind.Array)
                {
                    excerpts.AddRange(excerptEl.EnumerateArray().Select(static value => value.GetString() ?? ""));
                }

                candidates.Add(new ThreadCandidate
                {
                    ThreadId = item.GetProperty("thread_id").GetString() ?? "",
                    Title = item.GetProperty("title").GetString() ?? "",
                    Excerpts = excerpts,
                    IssueKey = item.TryGetProperty("issue_key", out var key) && key.ValueKind == JsonValueKind.String
                        ? key.GetString()
                        : null
                });
            }
        }

        var attachments = new List<string>();
        if (root.TryGetProperty("attachment_names", out var names) && names.ValueKind == JsonValueKind.Array)
        {
            attachments.AddRange(names.EnumerateArray().Select(static value => value.GetString() ?? ""));
        }

        return new InferenceRequest
        {
            EntryId = root.GetProperty("entry_id").GetString() ?? "",
            NoteText = root.TryGetProperty("note_text", out var note) ? note.GetString() ?? "" : "",
            AttachmentNames = attachments,
            Candidates = candidates,
            EntryRevision = "eval-rev",
            CorrectionRevision = 0,
            PolicyRevision = 1
        };
    }

    private sealed class EvalCase
    {
        public EvalCase(InferenceRequest request, JsonElement expected)
        {
            Request = request;
            ExpectedDecision = expected.GetProperty("expected_decision").GetString() ?? "abstain";
            ExpectedThreadId = expected.TryGetProperty("expected_thread_id", out var thread) && thread.ValueKind == JsonValueKind.String
                ? thread.GetString()
                : null;
            ExpectedRole = expected.TryGetProperty("expected_role", out var role) && role.ValueKind == JsonValueKind.String
                ? role.GetString()
                : null;
            Determinate = expected.TryGetProperty("determinate", out var det) && det.ValueKind == JsonValueKind.True;
            Critical = expected.TryGetProperty("critical", out var crit) && crit.ValueKind == JsonValueKind.True;
        }

        public InferenceRequest Request { get; }
        public string ExpectedDecision { get; }
        public string? ExpectedThreadId { get; }
        public string? ExpectedRole { get; }
        public bool Determinate { get; }
        public bool Critical { get; }
    }
}
