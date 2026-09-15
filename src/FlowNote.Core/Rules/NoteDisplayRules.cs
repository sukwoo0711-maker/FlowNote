namespace FlowNote.Core.Rules;

public static class NoteDisplayRules
{
    public static string DisplayPreview(string title, string preview)
    {
        if (string.IsNullOrWhiteSpace(preview))
        {
            return "";
        }

        if (string.Equals(preview.Trim(), title.Trim(), StringComparison.Ordinal))
        {
            return "";
        }

        var prefix = title + "\n";
        if (preview.StartsWith(prefix, StringComparison.Ordinal))
        {
            return preview[prefix.Length..];
        }

        return preview;
    }

    public static bool ShowKindChrome(string kindLabel, bool isEvent, bool isGroup)
    {
        if (isGroup)
        {
            return false;
        }

        if (!isEvent && kindLabel == "메모")
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(kindLabel);
    }

    public static string NoteCardTitle(string? titleSnapshot, string body, bool hasImage, bool hasFiles)
    {
        if (!string.IsNullOrWhiteSpace(titleSnapshot))
        {
            return titleSnapshot;
        }

        if (string.IsNullOrWhiteSpace(body) && hasImage)
        {
            return "이미지 기록";
        }

        if (string.IsNullOrWhiteSpace(body) && hasFiles)
        {
            return "파일 기록";
        }

        return "메모";
    }
}
