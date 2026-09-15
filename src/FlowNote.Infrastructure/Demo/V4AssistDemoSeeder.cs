using System.Globalization;
using System.Text.Json;
using FlowNote.Core.Assist;
using FlowNote.Core.Models;
using FlowNote.Core.Time;

namespace FlowNote.Infrastructure.Demo;

public static class V4AssistDemoSeeder
{
    public const string SettingKey = "demo.v4.assist";
    public const string SettingValue = "1";

    public static async Task EnsureAsync(FlowNoteDatabase database, Action<DateTimeOffset> setClock, string? fixtureDirectory)
    {
        if (database.Settings.Get(SettingKey) == SettingValue)
        {
            return;
        }

        await SeedDeferredAsync(database, setClock, fixtureDirectory);
        database.Settings.Set(SettingKey, SettingValue);
    }

    public static async Task<IReadOnlyDictionary<string, string>> SeedDeferredAsync(
        FlowNoteDatabase database,
        Action<DateTimeOffset> setClock,
        string? fixtureDirectory)
    {
        var inputsPath = Resolve(fixtureDirectory, "A_deferred.inputs.json");
        var expectedPath = Resolve(fixtureDirectory, "A_deferred.expected.json");
        if (inputsPath is null || expectedPath is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        using var inputs = JsonDocument.Parse(await File.ReadAllTextAsync(inputsPath));
        using var expected = JsonDocument.Parse(await File.ReadAllTextAsync(expectedPath));
        var idMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var ev in inputs.RootElement.GetProperty("events").EnumerateArray())
        {
            var fixtureId = ev.GetProperty("entry_id").GetString() ?? "";
            var occurred = DateTimeOffset.Parse(ev.GetProperty("occurred_at").GetString()!, CultureInfo.InvariantCulture);
            setClock(occurred.ToUniversalTime());
            var saved = await database.Entries.SaveNoteAsync(new SaveNoteRequest
            {
                RequestId = "assist-demo-" + fixtureId,
                Body = ev.GetProperty("note_text").GetString() ?? "",
                OccurredAtUtc = occurred.ToUniversalTime()
            });
            idMap[fixtureId] = saved.Id;
        }

        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var assignment in expected.RootElement.GetProperty("assignments").EnumerateArray())
        {
            var fixtureEntry = assignment.GetProperty("entry_id").GetString() ?? "";
            var alias = assignment.GetProperty("thread_alias").GetString() ?? "";
            var role = AssistCodec.ParseRole(assignment.GetProperty("role").GetString());
            if (!idMap.TryGetValue(fixtureEntry, out var realId))
            {
                continue;
            }

            var entry = await database.Entries.GetByIdAsync(realId);
            if (entry is null)
            {
                continue;
            }

            if (!aliases.TryGetValue(alias, out var threadId))
            {
                var created = database.Assist.CreateThread(
                    Truncate(entry.Body),
                    entry.Id,
                    AssistText.EntryRevision(entry),
                    TitleOrigin.Derived);
                threadId = created.Id;
                aliases[alias] = threadId;
            }

            database.Assist.ApplySynthetic(entry, new InferenceResult
            {
                EntryId = entry.Id,
                Decision = AssistDecision.Link,
                Primary = new InferencePrimary
                {
                    ThreadId = threadId,
                    TopicQuote = null,
                    Role = role,
                    SourceQuote = entry.Body.Length <= AssistVersions.SourceQuoteMax ? entry.Body : entry.Body[..AssistVersions.SourceQuoteMax],
                    NextActionQuote = null
                },
                IsFake = true
            }, AssignmentOrigin.Rule);
        }

        return idMap;
    }

    private static string Truncate(string body)
    {
        var line = body.Replace("\r\n", "\n", StringComparison.Ordinal).Trim().Split('\n')[0];
        return line.Length <= 40 ? line : line[..40];
    }

    private static string? Resolve(string? fixtureDirectory, string fileName)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(fixtureDirectory))
        {
            candidates.Add(Path.Combine(fixtureDirectory, fileName));
            candidates.Add(Path.Combine(fixtureDirectory, "scenarios", fileName));
        }

        var start = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && start is not null; i++)
        {
            candidates.Add(Path.Combine(start.FullName, "fixtures", "assist-v4", "scenarios", fileName));
            candidates.Add(Path.Combine(start.FullName, "FlowNote_Local_Assist_V4", "fixtures", "scenarios", fileName));
            start = start.Parent;
        }

        return candidates.FirstOrDefault(File.Exists);
    }
}
