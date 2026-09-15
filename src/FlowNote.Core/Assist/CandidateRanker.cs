using System.Globalization;
using System.Text;

namespace FlowNote.Core.Assist;

public static class CandidateRanker
{
    public static double DiceBigram(string left, string right)
    {
        var a = Bigrams(Normalize(left));
        var b = Bigrams(Normalize(right));
        if (a.Count == 0 || b.Count == 0)
        {
            return 0;
        }

        var intersection = 0;
        foreach (var (key, count) in a)
        {
            if (b.TryGetValue(key, out var other))
            {
                intersection += Math.Min(count, other);
            }
        }

        return 2.0 * intersection / (a.Values.Sum() + b.Values.Sum());
    }

    public static string Normalize(string value)
        => value.Normalize(NormalizationForm.FormC).ToLower(CultureInfo.InvariantCulture);

    private static Dictionary<string, int> Bigrams(string text)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        if (text.Length < 2)
        {
            return map;
        }

        for (var i = 0; i < text.Length - 1; i++)
        {
            var gram = text.Substring(i, 2);
            map[gram] = map.GetValueOrDefault(gram) + 1;
        }

        return map;
    }
}
