namespace FlowNote.Core.Assist;

public sealed class RulesEngine
{
    public InferenceResult Decide(InferenceRequest request, bool userLocked, bool manualClear)
    {
        if (userLocked || manualClear)
        {
            return Abstain(request.EntryId, "user-lock");
        }

        var keys = AssistText.IssueKeys(request.NoteText);
        var unique = keys
            .Select(key => request.Candidates.Where(item => string.Equals(item.IssueKey, key, StringComparison.OrdinalIgnoreCase)).ToList())
            .Where(static list => list.Count == 1)
            .Select(static list => list[0])
            .DistinctBy(static item => item.ThreadId)
            .ToList();
        if (keys.Count == 1 && unique.Count == 1)
        {
            var hit = unique[0];
            return new InferenceResult
            {
                EntryId = request.EntryId,
                Decision = AssistDecision.Link,
                Primary = new InferencePrimary
                {
                    ThreadId = hit.ThreadId,
                    TopicQuote = null,
                    Role = ContextRole.Unknown,
                    SourceQuote = keys[0],
                    NextActionQuote = null
                },
                IsFake = false
            };
        }

        if (keys.Count > 1)
        {
            return Abstain(request.EntryId, "multiple-issue-keys");
        }

        var aliasHits = request.Candidates
            .Where(item => item.Aliases.Any(alias =>
                !string.IsNullOrWhiteSpace(alias) &&
                request.NoteText.Contains(alias, StringComparison.Ordinal)))
            .DistinctBy(static item => item.ThreadId)
            .ToList();
        if (aliasHits.Count == 1)
        {
            var hit = aliasHits[0];
            var quote = hit.Aliases.First(alias => request.NoteText.Contains(alias, StringComparison.Ordinal));
            return new InferenceResult
            {
                EntryId = request.EntryId,
                Decision = AssistDecision.Link,
                Primary = new InferencePrimary
                {
                    ThreadId = hit.ThreadId,
                    TopicQuote = null,
                    Role = ContextRole.Unknown,
                    SourceQuote = quote,
                    NextActionQuote = null
                },
                IsFake = false
            };
        }

        if (aliasHits.Count > 1)
        {
            return Abstain(request.EntryId, "multiple-aliases");
        }

        return Abstain(request.EntryId, "uncertain");
    }

    public static InferenceResult Abstain(string entryId, string? error = null)
        => new()
        {
            EntryId = entryId,
            Decision = AssistDecision.Abstain,
            ErrorCode = error,
            IsFake = false
        };
}
