using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FlowNote.Core.Errors;
using FlowNote.Core.Models;

namespace FlowNote.Infrastructure.Reports;

public sealed class ReportZipWriter
{
    public void Write(ReportSnapshot snapshot, string zipPath, IReadOnlyDictionary<string, string> attachmentFullPaths)
    {
        var temp = zipPath + ".partial";
        if (File.Exists(temp))
        {
            File.Delete(temp);
        }

        using (var stream = File.Create(temp))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            WriteEntry(zip, "report.md", snapshot.Markdown);
            WriteEntry(zip, "manifest.json", Manifest(snapshot));
            foreach (var file in snapshot.Files)
            {
                if (!attachmentFullPaths.TryGetValue(file.AttachmentId, out var full) || !File.Exists(full))
                {
                    throw new ValidationException($"선택한 파일 '{file.OriginalName}'을 읽지 못했습니다.");
                }

                var bytes = File.ReadAllBytes(full);
                var actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
                if (!string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ValidationException($"파일 '{file.OriginalName}'이 미리보기 이후 변경되었습니다.");
                }

                var entry = zip.CreateEntry(file.RelativePath.Replace('\\', '/'), CompressionLevel.Fastest);
                using var dest = entry.Open();
                dest.Write(bytes);
            }
        }

        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        File.Move(temp, zipPath);
    }

    private static void WriteEntry(ZipArchive zip, string name, string text)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(text);
    }

    private static string Manifest(ReportSnapshot snapshot)
    {
        var payload = new
        {
            snapshot_id = snapshot.Id,
            created_at_utc = snapshot.CreatedAtUtc.ToString("o"),
            report_date = snapshot.ReportDate.ToString("yyyy-MM-dd"),
            timezone = snapshot.TimeZoneDisplay,
            fingerprint = snapshot.Fingerprint,
            entries = snapshot.Entries.Select(item => new
            {
                id = item.Id,
                kind = item.Kind.ToString(),
                occurred_local_date = item.OccurredLocalDate.ToString("yyyy-MM-dd"),
                out_of_range = item.OutOfRange,
                body_sha256 = item.BodyRevision,
                work_item_id = item.WorkItemId
            }),
            files = snapshot.Files.Select(item => new
            {
                id = item.AttachmentId,
                original_name = item.OriginalName,
                relative_path = item.RelativePath,
                sha256 = item.Sha256,
                byte_size = item.ByteSize
            })
        };
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
