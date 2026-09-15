using FlowNote.Core.Errors;

namespace FlowNote.Core.Rules;

public static class AppLimits
{
    public const int MaxNoteBodyLength = 20_000;
    public const int MaxWorkItemTitleLength = 200;
    public const int MaxAttachmentBytes = 25 * 1024 * 1024;
    public const int MaxAttachmentsPerEntry = 10;
    public const int MaxNextActionLength = 200;
}

public static class NoteRules
{
    public static void ValidateSave(string body, bool hasAttachments)
    {
        if (body.Length > AppLimits.MaxNoteBodyLength)
        {
            throw new ValidationException($"본문은 {AppLimits.MaxNoteBodyLength}자를 넘길 수 없습니다. 잘라서 저장하지 않습니다.");
        }

        if (!hasAttachments && string.IsNullOrWhiteSpace(body))
        {
            throw new ValidationException("공백만 있는 메모는 저장할 수 없습니다. 사진이나 파일만 있는 기록은 저장할 수 있습니다.");
        }
    }

    public static string? TitleSnapshot(string? title, string body)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        var line = body.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(static part => part.Trim())
            .FirstOrDefault(static part => part.Length > 0);
        if (line is null)
        {
            return null;
        }

        return line.Length <= 80 ? line : line[..80];
    }
}

public static class WorkItemRules
{
    public static string ValidateTitle(string title)
    {
        var trimmed = title.Trim();
        if (trimmed.Length == 0)
        {
            throw new ValidationException("할 일 제목은 비울 수 없습니다.");
        }

        if (trimmed.Length > AppLimits.MaxWorkItemTitleLength)
        {
            throw new ValidationException($"할 일 제목은 {AppLimits.MaxWorkItemTitleLength}자를 넘길 수 없습니다.");
        }

        return trimmed;
    }
}

public static class FileSizeDisplay
{
    public static string Format(long byteSize)
    {
        if (byteSize < 1024)
        {
            return $"{byteSize} B";
        }

        if (byteSize < 1024 * 1024)
        {
            return $"{byteSize / 1024.0:0.#} KB";
        }

        return $"{byteSize / (1024.0 * 1024):0.#} MB";
    }
}
