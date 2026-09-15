using FlowNote.Core.Errors;

namespace FlowNote.Core.Rules;

public static class AttachmentRules
{
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".com", ".msi", ".scr", ".ps1", ".js", ".jse", ".vbs", ".vbe", ".wsf", ".wsh",
        ".hta", ".cpl", ".msc", ".pif", ".lnk", ".url", ".reg", ".dll", ".application", ".appref-ms", ".msp"
    };

    public static void ValidateFile(string originalName, long byteSize, int currentCount)
    {
        if (currentCount >= AppLimits.MaxAttachmentsPerEntry)
        {
            throw new ValidationException($"한 기록에는 파일을 {AppLimits.MaxAttachmentsPerEntry}개까지 첨부할 수 있습니다.");
        }

        if (byteSize <= 0)
        {
            throw new ValidationException("빈 파일은 첨부할 수 없습니다.");
        }

        if (byteSize > AppLimits.MaxAttachmentBytes)
        {
            throw new ValidationException("파일당 25MB를 넘는 첨부는 저장할 수 없습니다.");
        }

        var extension = Path.GetExtension(originalName);
        if (BlockedExtensions.Contains(extension))
        {
            throw new ValidationException("실행 파일·스크립트·바로가기는 첨부할 수 없습니다. 확장자만으로 안전하다고 보장하지는 않습니다.");
        }
    }

    public static string GuessMediaType(string originalName)
    {
        return Path.GetExtension(originalName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".txt" => "text/plain",
            ".log" => "text/plain",
            ".md" => "text/markdown",
            ".csv" => "text/csv",
            ".tsv" => "text/tab-separated-values",
            ".html" or ".htm" => "text/html",
            ".rtf" => "application/rtf",
            ".xml" => "application/xml",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xlsb" => "application/vnd.ms-excel.sheet.binary.macroEnabled.12",
            ".pdf" => "application/pdf",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }

    public static bool IsImage(string mediaType) => mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
}
