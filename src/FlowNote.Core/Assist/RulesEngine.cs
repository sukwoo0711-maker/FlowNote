namespace FlowNote.Core.Assist;

public sealed class RulesEngine
{
    public InferenceResult Decide(InferenceRequest request, bool userLocked, bool manualClear)
    {
        if (userLocked || manualClear)
        {
            return Abstain(request.EntryId, "user-lock");
        }

        var deferred = AssistText.LooksDeferred(request.NoteText);
        var keys = AssistText.IssueKeys(request.NoteText);
        var unique = keys
            .Select(key => request.Candidates.Where(item => string.Equals(item.IssueKey, key, StringComparison.OrdinalIgnoreCase)).ToList())
            .Where(static list => list.Count == 1)
            .Select(static list => list[0])
            .DistinctBy(static item => item.ThreadId)
            .ToList();
        if (keys.Count == 1 && unique.Count == 1)
        {
            return Link(request.EntryId, unique[0].ThreadId, keys[0], deferred);
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
            return Link(request.EntryId, hit.ThreadId, quote, deferred);
        }

        if (aliasHits.Count > 1)
        {
            return Abstain(request.EntryId, "multiple-aliases");
        }

        var tokenHits = MatchTokens(request);
        if (tokenHits.Count == 1)
        {
            var hit = tokenHits[0];
            return Link(request.EntryId, hit.ThreadId, hit.Quote, deferred);
        }

        if (tokenHits.Count > 1)
        {
            return Abstain(request.EntryId, "uncertain");
        }

        if (!deferred
            && request.ContinuationThreadId is not null
            && request.Candidates.Any(item => item.ThreadId == request.ContinuationThreadId)
            && CanContinue(request))
        {
            var quote = AssistText.TopicQuote(request.NoteText) ?? FirstUsableQuote(request.NoteText);
            if (quote is not null)
            {
                return Link(request.EntryId, request.ContinuationThreadId, quote, deferred: false);
            }
        }

        var topic = AssistText.TopicQuote(request.NoteText);
        if (topic is null)
        {
            return Abstain(request.EntryId, "uncertain");
        }

        return new InferenceResult
        {
            EntryId = request.EntryId,
            Decision = AssistDecision.New,
            Primary = new InferencePrimary
            {
                TopicQuote = topic,
                Role = deferred ? ContextRole.RequestLater : ContextRole.Performed,
                SourceQuote = topic
            },
            IsFake = false
        };
    }

    public static InferenceResult Abstain(string entryId, string? error = null)
        => new()
        {
            EntryId = entryId,
            Decision = AssistDecision.Abstain,
            ErrorCode = error,
            IsFake = false
        };

    private static InferenceResult Link(string entryId, string threadId, string quote, bool deferred)
        => new()
        {
            EntryId = entryId,
            Decision = AssistDecision.Link,
            Primary = new InferencePrimary
            {
                ThreadId = threadId,
                TopicQuote = null,
                Role = deferred ? ContextRole.RequestLater : ContextRole.Performed,
                SourceQuote = quote,
                NextActionQuote = null
            },
            IsFake = false
        };

    private static IReadOnlyList<(string ThreadId, string Quote)> MatchTokens(InferenceRequest request)
    {
        var tokens = AssistText.TopicTokens(request.NoteText);
        if (tokens.Count == 0)
        {
            return [];
        }

        var hits = new List<(string ThreadId, string Quote)>();
        foreach (var candidate in request.Candidates)
        {
            var fields = candidate.Aliases
                .Concat(candidate.Excerpts)
                .Append(candidate.Title);
            string? quote = null;
            foreach (var token in tokens)
            {
                if (token.Length < 2)
                {
                    continue;
                }

                if (fields.Any(field =>
                        !string.IsNullOrWhiteSpace(field)
                        && (field.Contains(token, StringComparison.Ordinal)
                            || request.NoteText.Contains(field, StringComparison.Ordinal))))
                {
                    quote = token;
                    break;
                }
            }

            if (quote is not null && hits.All(item => item.ThreadId != candidate.ThreadId))
            {
                hits.Add((candidate.ThreadId, quote));
            }
        }

        return hits;
    }

    private static bool CanContinue(InferenceRequest request)
    {
        var tokens = AssistText.TopicTokens(request.NoteText);
        if (tokens.Count == 0)
        {
            return true;
        }

        if (tokens.All(AssistText.IsFollowUpToken))
        {
            return true;
        }

        var current = request.Candidates.First(item => item.ThreadId == request.ContinuationThreadId);
        var fields = current.Aliases.Concat(current.Excerpts).Append(current.Title);
        return tokens.Any(token => fields.Any(field =>
            !string.IsNullOrWhiteSpace(field)
            && (field.Contains(token, StringComparison.Ordinal)
                || request.NoteText.Contains(field, StringComparison.Ordinal))));
    }

    private static string? FirstUsableQuote(string text)
    {
        var topic = AssistText.TopicQuote(text);
        if (topic is not null)
        {
            return topic;
        }

        var trimmed = (text ?? "").Trim();
        if (trimmed.Length >= 2 && trimmed.Length <= AssistVersions.SourceQuoteMax && !AssistText.IsGenericTopic(trimmed))
        {
            return trimmed.Length <= 24 ? trimmed : trimmed[..24];
        }

        return null;
    }
}
