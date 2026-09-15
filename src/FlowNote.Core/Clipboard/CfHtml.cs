using System.Text;
using System.Text.RegularExpressions;

namespace FlowNote.Core.Clipboard;

public static class CfHtml
{
    private static readonly Regex OffsetLine = new(
        @"^(StartHTML|EndHTML|StartFragment|EndFragment):(?<value>\d+)",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    public static string ToSavableDocument(string raw)
    {
        var fragment = ExtractFragment(raw);
        if (LooksLikeDocument(fragment))
        {
            return fragment;
        }

        return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head><body>\n"
            + fragment
            + "\n</body></html>\n";
    }

    public static string ExtractFragment(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "";
        }

        var startMark = raw.IndexOf("<!--StartFragment-->", StringComparison.OrdinalIgnoreCase);
        var endMark = raw.IndexOf("<!--EndFragment-->", StringComparison.OrdinalIgnoreCase);
        if (startMark >= 0 && endMark > startMark)
        {
            return raw[(startMark + "<!--StartFragment-->".Length)..endMark].Trim();
        }

        var offsets = ReadOffsets(raw);
        if (offsets.TryGetValue("StartFragment", out var start)
            && offsets.TryGetValue("EndFragment", out var end)
            && end > start
            && end <= raw.Length)
        {
            return raw[start..end].Trim();
        }

        if (offsets.TryGetValue("StartHTML", out start)
            && offsets.TryGetValue("EndHTML", out end)
            && end > start
            && end <= raw.Length)
        {
            return raw[start..end].Trim();
        }

        var firstTag = raw.IndexOf('<');
        return firstTag >= 0 ? raw[firstTag..].Trim() : raw.Trim();
    }

    public static bool HasTable(string html)
        => html.Contains("<table", StringComparison.OrdinalIgnoreCase)
           || html.Contains("urn:schemas-microsoft-com:office:excel", StringComparison.OrdinalIgnoreCase);

    public static bool HasImage(string html)
        => html.Contains("<img", StringComparison.OrdinalIgnoreCase);

    public static bool IsPlainWrapper(string html, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var stripped = StripTags(ExtractFragment(html));
        return Normalize(stripped) == Normalize(text);
    }

    public static string StripTags(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return "";
        }

        var builder = new StringBuilder(html.Length);
        var inside = false;
        foreach (var ch in html)
        {
            if (ch == '<')
            {
                inside = true;
                continue;
            }

            if (ch == '>')
            {
                inside = false;
                continue;
            }

            if (!inside)
            {
                builder.Append(ch);
            }
        }

        return System.Net.WebUtility.HtmlDecode(builder.ToString());
    }

    private static Dictionary<string, int> ReadOffsets(string raw)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in OffsetLine.Matches(raw))
        {
            if (int.TryParse(match.Groups["value"].Value, out var value))
            {
                result[match.Groups[1].Value] = value;
            }
        }

        return result;
    }

    private static bool LooksLikeDocument(string html)
        => html.Contains("<html", StringComparison.OrdinalIgnoreCase)
           || html.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value)
        => string.Join(
            " ",
            value.Replace('\u00a0', ' ').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
