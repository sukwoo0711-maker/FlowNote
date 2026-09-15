using System.Text;

namespace FlowNote.Core.Clipboard;

public static class ClipboardImportPlanner
{
    public static ClipboardImportPlan Plan(ClipboardOffer offer, string fileStamp)
    {
        var stamp = string.IsNullOrWhiteSpace(fileStamp) ? "paste" : fileStamp;
        var files = offer.FilePaths
            .Where(static path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (files.Count > 0)
        {
            return new ClipboardImportPlan
            {
                ExistingFilePaths = files,
                InsertText = CaptionOrNull(offer.Text)
            };
        }

        if (IsSpreadsheetXml(offer.XmlSpreadsheet))
        {
            return AttachmentOnly(Part($"paste-{stamp}.xml", "application/vnd.ms-excel", Utf8(offer.XmlSpreadsheet!)), offer.Text);
        }

        if (HasBytes(offer.Biff12))
        {
            return AttachmentOnly(Part($"paste-{stamp}.xlsb", "application/vnd.ms-excel.sheet.binary.macroEnabled.12", offer.Biff12!), offer.Text);
        }

        if (HasBytes(offer.Biff8))
        {
            return AttachmentOnly(Part($"paste-{stamp}.xls", "application/vnd.ms-excel", offer.Biff8!), offer.Text);
        }

        if (!string.IsNullOrWhiteSpace(offer.Html) && IsRichHtml(offer.Html, offer.Text, offer.Png))
        {
            return AttachmentOnly(
                Part($"paste-{stamp}.html", "text/html", Utf8(CfHtml.ToSavableDocument(offer.Html))),
                offer.Text);
        }

        if (!string.IsNullOrWhiteSpace(offer.Rtf) && IsRichRtf(offer.Rtf))
        {
            return AttachmentOnly(Part($"paste-{stamp}.rtf", "application/rtf", Utf8(offer.Rtf)), offer.Text);
        }

        if (HasBytes(offer.Png))
        {
            return AttachmentOnly(Part($"paste-{stamp}.png", "image/png", offer.Png!), offer.Text);
        }

        if (!string.IsNullOrWhiteSpace(offer.Csv) && LooksLikeTable(offer.Csv))
        {
            return AttachmentOnly(Part($"paste-{stamp}.csv", "text/csv", Utf8(offer.Csv)), offer.Text);
        }

        if (!string.IsNullOrWhiteSpace(offer.Text) && LooksLikeTable(offer.Text))
        {
            var name = offer.Text.Contains('\t', StringComparison.Ordinal) ? $"paste-{stamp}.tsv" : $"paste-{stamp}.csv";
            var media = name.EndsWith(".tsv", StringComparison.Ordinal) ? "text/tab-separated-values" : "text/csv";
            return new ClipboardImportPlan
            {
                Attachments = [Part(name, media, Utf8(offer.Text))]
            };
        }

        return new ClipboardImportPlan
        {
            InsertText = string.IsNullOrEmpty(offer.Text) ? null : offer.Text
        };
    }

    private static ClipboardImportPlan AttachmentOnly(ClipboardImportAttachment part, string? text)
        => new()
        {
            Attachments = [part],
            InsertText = CaptionOrNull(text)
        };

    private static ClipboardImportAttachment Part(string fileName, string mediaType, byte[] bytes)
        => new()
        {
            FileName = fileName,
            MediaType = mediaType,
            Bytes = bytes
        };

    private static bool IsSpreadsheetXml(string? xml)
        => !string.IsNullOrWhiteSpace(xml)
           && (xml.Contains("Spreadsheet", StringComparison.OrdinalIgnoreCase)
               || xml.Contains("urn:schemas-microsoft-com:office:spreadsheet", StringComparison.OrdinalIgnoreCase));

    private static bool IsRichHtml(string html, string? text, byte[]? png)
    {
        if (CfHtml.HasTable(html))
        {
            return true;
        }

        if (CfHtml.IsPlainWrapper(html, text))
        {
            return false;
        }

        return CfHtml.HasImage(html) && !HasBytes(png);
    }

    private static bool IsRichRtf(string rtf)
        => rtf.Contains(@"\trowd", StringComparison.Ordinal)
           || rtf.Contains(@"\cell", StringComparison.Ordinal)
           || rtf.Contains(@"\pict", StringComparison.Ordinal);

    public static bool LooksLikeTable(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 2)
        {
            return false;
        }

        if (lines.Count(static line => line.Contains('\t')) >= 2)
        {
            return true;
        }

        return lines.Count(static line => line.Split(',').Length >= 3) >= 2;
    }

    private static string? CaptionOrNull(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || LooksLikeTable(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        if (trimmed.Length > 200)
        {
            return null;
        }

        var lines = trimmed.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        return lines.Length == 1 ? trimmed : null;
    }

    private static bool HasBytes(byte[]? data) => data is { Length: > 0 };

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);
}
