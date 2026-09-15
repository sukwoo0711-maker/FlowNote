using FlowNote.Core.Models;
using FlowNote.Core.Rules;

namespace FlowNote.Core.Assist;

public static class AssistText
{
    public static string EntryRevision(TimelineEntry entry)
        => ContentRevision.Sha256Hex(entry.Body + "|" + (entry.UpdatedAtUtc?.ToString("O") ?? "") + "|" + entry.OccurredAtUtc.ToString("O"));

    public static bool IsGenericTopic(string quote)
    {
        var t = quote.Trim();
        if (t.Length is < 2 or > AssistVersions.TopicQuoteMax)
        {
            return true;
        }

        return t is "확인" or "완료" or "테스트" or "작업" or "메모" or "그거" or "진행" or "첨부" or "자료" or "로그";
    }

    public static bool IsExactSubstring(string source, string? quote)
    {
        if (string.IsNullOrEmpty(quote))
        {
            return false;
        }

        return source.Contains(quote, StringComparison.Ordinal);
    }

    public static IReadOnlyList<string> IssueKeys(string text)
    {
        var matches = new List<string>();
        var span = text.AsSpan();
        var i = 0;
        while (i < span.Length)
        {
            if (char.IsLetter(span[i]) && char.IsUpper(span[i]))
            {
                var start = i;
                while (i < span.Length && char.IsLetter(span[i]) && char.IsUpper(span[i]))
                {
                    i++;
                }

                if (i - start >= 2 && i < span.Length && span[i] == '-')
                {
                    i++;
                    var digitStart = i;
                    while (i < span.Length && char.IsDigit(span[i]))
                    {
                        i++;
                    }

                    if (i > digitStart)
                    {
                        matches.Add(text[start..i]);
                        continue;
                    }
                }
            }

            i++;
        }

        return matches.Distinct(StringComparer.Ordinal).ToList();
    }
}
