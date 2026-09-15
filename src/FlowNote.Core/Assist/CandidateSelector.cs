namespace FlowNote.Core.Assist;

public static class CandidateSelector
{
    public static IReadOnlyList<ThreadCandidate> Rank(string noteText, IReadOnlyList<ThreadCandidate> pool, int max = AssistVersions.MaxCandidates)
    {
        var keys = AssistText.IssueKeys(noteText);
        var selected = new List<ThreadCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in pool)
        {
            if (candidate.IssueKey is not null &&
                keys.Any(key => string.Equals(key, candidate.IssueKey, StringComparison.OrdinalIgnoreCase)) &&
                seen.Add(candidate.ThreadId))
            {
                selected.Add(candidate);
            }
        }

        foreach (var candidate in pool)
        {
            if (candidate.Aliases.Any(alias =>
                    !string.IsNullOrWhiteSpace(alias) &&
                    noteText.Contains(alias, StringComparison.Ordinal)) &&
                seen.Add(candidate.ThreadId))
            {
                selected.Add(candidate);
            }
        }

        var ranked = pool
            .Where(item => !seen.Contains(item.ThreadId))
            .Select(item => new
            {
                Item = item,
                Score = Math.Max(
                    CandidateRanker.DiceBigram(noteText, item.Title),
                    item.Excerpts.Select(excerpt => CandidateRanker.DiceBigram(noteText, excerpt)).DefaultIfEmpty(0).Max())
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Item.ThreadId, StringComparer.Ordinal)
            .Select(item => item.Item);

        foreach (var candidate in ranked)
        {
            if (selected.Count >= max)
            {
                break;
            }

            if (seen.Add(candidate.ThreadId))
            {
                selected.Add(candidate);
            }
        }

        return selected.Take(max).ToList();
    }

    public static IReadOnlyList<ThreadCandidate> FitBudget(string noteText, IReadOnlyList<ThreadCandidate> candidates, int maxUtf8Bytes)
    {
        var current = candidates.Select(item => new ThreadCandidate
        {
            ThreadId = item.ThreadId,
            Title = item.Title,
            IssueKey = item.IssueKey,
            Aliases = item.Aliases,
            Excerpts = item.Excerpts
        }).ToList();

        while (current.Count > 0 && Measure(noteText, current) > maxUtf8Bytes)
        {
            var last = current[^1];
            if (last.Excerpts.Count > 0)
            {
                current[^1] = new ThreadCandidate
                {
                    ThreadId = last.ThreadId,
                    Title = last.Title,
                    IssueKey = last.IssueKey,
                    Aliases = last.Aliases,
                    Excerpts = last.Excerpts.Take(last.Excerpts.Count - 1).ToList()
                };
                continue;
            }

            current.RemoveAt(current.Count - 1);
        }

        return current;
    }

    public static int Measure(string noteText, IReadOnlyList<ThreadCandidate> candidates)
    {
        var payload = "{\"note_text\":\"" + noteText + "\",\"candidates\":" + candidates.Count + "}";
        foreach (var candidate in candidates)
        {
            payload += candidate.Title + string.Join('\n', candidate.Excerpts);
        }

        return System.Text.Encoding.UTF8.GetByteCount(payload);
    }
}
