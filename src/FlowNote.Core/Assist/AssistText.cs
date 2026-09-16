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

    public static bool IsFollowUpToken(string token)
        => token is "확인" or "완료" or "테스트" or "작업" or "메모" or "그거" or "진행"
            or "첨부" or "자료" or "로그" or "초기화" or "순서" or "재현" or "조건"
            or "변경" or "점검" or "출력" or "비교" or "재시작" or "재시도" or "원인"
            or "결과" or "상태" or "설정" or "측정" or "추가" or "다시" or "후속"
            or "시작" or "검토" or "중" or "하나" or "더";

    public static bool IsExactSubstring(string source, string? quote)
    {
        if (string.IsNullOrEmpty(quote))
        {
            return false;
        }

        return source.Contains(quote, StringComparison.Ordinal);
    }

    public static bool LooksDeferred(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("나중에", StringComparison.Ordinal)
            || (text.Contains("요청", StringComparison.Ordinal)
                && (text.Contains("들어", StringComparison.Ordinal)
                    || text.Contains("보기", StringComparison.Ordinal)
                    || text.Contains("부탁", StringComparison.Ordinal)));
    }

    public static bool LooksPlan(string text)
        => ContainsAny(text, "예정", "내일", "모레", "계획");

    public static bool LooksRequest(string text)
        => ContainsAny(text, "요청", "부탁") && !LooksDeferred(text);

    public static bool LooksCompletionMention(string text)
    {
        if (string.IsNullOrWhiteSpace(text)
            || ContainsAny(text, "재현 안", "아직", "할 수 없", "완료하지", "끝내지", "마치지", "못했", "못함"))
        {
            return false;
        }

        return ContainsAny(text, "완료했", "끝냈", "마쳤", "처리했");
    }

    public static bool LooksPerformed(string text)
        => ContainsAny(text, "확인 중", "검토 시작", "검토 중", "재현", "점검 중", "처리 중", "하고 있");

    public static bool LooksMixedPerformAndDefer(string text)
        => LooksDeferred(text) && LooksPerformed(text);

    public static IReadOnlyList<string> Clauses(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Split(['.', '。', '!', '?', ';', ',', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    public static ContextRole ClassifyRole(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ContextRole.Unknown;
        }

        if (LooksMixedPerformAndDefer(text))
        {
            return ContextRole.Performed;
        }

        if (LooksDeferred(text))
        {
            return ContextRole.RequestLater;
        }

        if (LooksPlan(text))
        {
            return ContextRole.Plan;
        }

        if (LooksCompletionMention(text))
        {
            return ContextRole.CompletionMention;
        }

        if (LooksRequest(text))
        {
            return ContextRole.RequestUnknown;
        }

        if (LooksPerformed(text))
        {
            return ContextRole.Performed;
        }

        return ContextRole.Unknown;
    }

    public static bool IsAutoAlias(string alias)
        => string.IsNullOrWhiteSpace(alias)
            || alias.Length < 3
            || IsGenericTopic(alias)
            || IsFollowUpToken(alias);

    private static bool ContainsAny(string text, params string[] parts)
        => parts.Any(part => text.Contains(part, StringComparison.Ordinal));

    public static IReadOnlyList<string> TopicTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var tokens = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in Tokenize(text))
        {
            if (IsGenericTopic(raw) || raw.Length < 2 || !seen.Add(raw))
            {
                continue;
            }

            tokens.Add(raw);
        }

        return tokens;
    }

    public static string? TopicQuote(string text)
    {
        var tokens = TopicTokens(text);
        if (tokens.Count == 0)
        {
            return null;
        }

        if (tokens.Count >= 2)
        {
            var pair = tokens[0] + " " + tokens[1];
            if (text.Contains(pair, StringComparison.Ordinal) && !IsGenericTopic(pair))
            {
                return pair.Length <= AssistVersions.TopicQuoteMax ? pair : tokens[0];
            }
        }

        return tokens[0];
    }

    public static IReadOnlyList<string> AliasSeeds(string text)
        => TopicTokens(text).Where(static item => item.Length >= 3).ToList();

    private static IEnumerable<string> Tokenize(string text)
    {
        var buffer = new System.Text.StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch) || ch is >= '가' and <= '힣')
            {
                buffer.Append(ch);
                continue;
            }

            if (buffer.Length > 0)
            {
                yield return buffer.ToString();
                buffer.Clear();
            }
        }

        if (buffer.Length > 0)
        {
            yield return buffer.ToString();
        }
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
