using System.Text;
using FlowNote.Core.Errors;
using FlowNote.Core.Models;

namespace FlowNote.Core.Rules;

public static class ReportBuilder
{
    public static ReportSnapshot Build(ReportBuildRequest request, DateTimeOffset nowUtc)
    {
        var selected = new HashSet<string>(request.SelectedIds, StringComparer.Ordinal);
        var selectedFiles = new HashSet<string>(request.SelectedAttachmentIds, StringComparer.Ordinal);
        if (selected.Count == 0)
        {
            throw new ValidationException("선택한 기록이 없습니다. 출력할 원문을 고르세요.");
        }

        var byId = request.Catalog.ToDictionary(static item => item.Id, StringComparer.Ordinal);
        var entries = new List<ReportSnapshotEntry>();
        foreach (var id in request.SelectedIds)
        {
            if (!byId.TryGetValue(id, out var candidate))
            {
                throw new ValidationException("선택한 기록을 목록에서 찾지 못했습니다.");
            }

            entries.Add(new ReportSnapshotEntry
            {
                Id = candidate.Id,
                Kind = candidate.Kind,
                OccurredLocalDate = candidate.OccurredLocalDate,
                Body = candidate.Body,
                BodyRevision = candidate.BodyRevision,
                Title = candidate.Title,
                WorkItemId = candidate.WorkItemId,
                WorkTitle = candidate.WorkTitle,
                OutOfRange = candidate.OutOfRange,
                Section = Classify(candidate),
                AssistProvenance = candidate.AssistProvenance
            });
        }

        var files = new List<ReportSnapshotFile>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parentIds = new HashSet<string>(entries.Select(static item => item.Id), StringComparer.Ordinal);
        foreach (var candidate in request.Catalog)
        {
            if (!parentIds.Contains(candidate.Id))
            {
                continue;
            }

            foreach (var file in candidate.Files)
            {
                if (!selectedFiles.Contains(file.AttachmentId))
                {
                    continue;
                }

                var safe = SafeFileName(file.OriginalName, usedNames);
                files.Add(new ReportSnapshotFile
                {
                    AttachmentId = file.AttachmentId,
                    EntryId = file.EntryId,
                    OriginalName = file.OriginalName,
                    SafeName = safe,
                    RelativePath = "attachments/" + safe,
                    Sha256 = file.Sha256,
                    ByteSize = file.ByteSize
                });
            }
        }

        var markdown = WriteMarkdown(request, entries, files);
        var fingerprint = Fingerprint(entries, files);
        return new ReportSnapshot
        {
            Id = Guid.NewGuid().ToString("D"),
            CreatedAtUtc = nowUtc,
            ReportDate = request.ReportDate,
            TimeZoneDisplay = request.TimeZoneDisplay,
            Markdown = markdown,
            Fingerprint = fingerprint,
            Entries = entries,
            Files = files
        };
    }

    public static bool IsStale(ReportSnapshot snapshot, IReadOnlyList<ReportCandidate> catalog)
    {
        var byId = catalog.ToDictionary(static item => item.Id, StringComparer.Ordinal);
        foreach (var entry in snapshot.Entries)
        {
            if (!byId.TryGetValue(entry.Id, out var current))
            {
                return true;
            }

            if (!string.Equals(current.BodyRevision, entry.BodyRevision, StringComparison.Ordinal))
            {
                return true;
            }
        }

        var filesById = catalog
            .SelectMany(static item => item.Files)
            .GroupBy(static item => item.AttachmentId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
        foreach (var file in snapshot.Files)
        {
            if (!filesById.TryGetValue(file.AttachmentId, out var current))
            {
                return true;
            }

            if (!string.Equals(current.Sha256, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string CurrentFingerprint(IReadOnlyList<ReportSnapshotEntry> entries, IReadOnlyList<ReportSnapshotFile> files)
        => Fingerprint(entries, files);

    public static string SafeFileName(string original, HashSet<string> used)
    {
        var name = Path.GetFileName(original.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(name) || name is "." or "..")
        {
            name = "file";
        }

        var builder = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            if (ch is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|' or '\0')
            {
                builder.Append('_');
            }
            else
            {
                builder.Append(ch);
            }
        }

        var sanitized = builder.ToString();
        if (sanitized.Contains("..", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("..", "_", StringComparison.Ordinal);
        }

        var unique = sanitized;
        var index = 1;
        var stem = Path.GetFileNameWithoutExtension(sanitized);
        var ext = Path.GetExtension(sanitized);
        while (!used.Add(unique))
        {
            unique = $"{stem}-{index}{ext}";
            index++;
        }

        return unique;
    }

    private static string Classify(ReportCandidate candidate)
    {
        if (candidate.OutOfRange)
        {
            return "참고 기록";
        }

        return candidate.Kind switch
        {
            ReportCandidateKind.LifecycleCompleted => "완료한 일",
            ReportCandidateKind.NextAction => "다음 행동",
            ReportCandidateKind.Note when candidate.WorkStatusAsOf == WorkItemStatus.Open => "남은 업무의 기록",
            ReportCandidateKind.Note when candidate.WorkStatusAsOf == WorkItemStatus.Completed => "완료한 일",
            _ => "남긴 기록"
        };
    }

    private static string WriteMarkdown(
        ReportBuildRequest request,
        IReadOnlyList<ReportSnapshotEntry> entries,
        IReadOnlyList<ReportSnapshotFile> files)
    {
        var text = new StringBuilder();
        text.AppendLine($"# {request.ReportDate:yyyy-MM-dd} 하루 정리");
        text.AppendLine();
        text.AppendLine($"표시 시간대: {request.TimeZoneDisplay}");
        text.AppendLine();

        foreach (var section in new[] { "완료한 일", "남은 업무의 기록", "다음 행동", "남긴 기록", "참고 기록" })
        {
            var items = entries.Where(item => item.Section == section).ToList();
            if (items.Count == 0)
            {
                continue;
            }

            text.AppendLine($"## {section}");
            text.AppendLine();
            foreach (var item in items)
            {
                var heading = string.IsNullOrWhiteSpace(item.WorkTitle) ? item.Title : item.WorkTitle;
                if (string.IsNullOrWhiteSpace(heading))
                {
                    heading = item.Kind == ReportCandidateKind.NextAction ? "다음 행동" : "기록";
                }

                if (item.OutOfRange)
                {
                    text.AppendLine($"### {item.OccurredLocalDate:yyyy-MM-dd} · {heading}");
                }
                else
                {
                    text.AppendLine($"### {heading}");
                }

                text.AppendLine();
                text.AppendLine(string.IsNullOrWhiteSpace(item.Body) ? "_(본문 없음)_" : item.Body);
                text.AppendLine();
                if (!string.IsNullOrWhiteSpace(item.AssistProvenance))
                {
                    text.AppendLine("파생 연결: " + item.AssistProvenance);
                    text.AppendLine();
                }
                var linked = files.Where(file => file.EntryId == item.Id).ToList();
                if (linked.Count > 0)
                {
                    text.AppendLine("포함 파일:");
                    foreach (var file in linked)
                    {
                        text.AppendLine($"- [{file.OriginalName}]({file.RelativePath})");
                    }

                    text.AppendLine();
                }
            }
        }

        return text.ToString();
    }

    private static string Fingerprint(IReadOnlyList<ReportSnapshotEntry> entries, IReadOnlyList<ReportSnapshotFile> files)
    {
        var raw = string.Join('|',
            entries.Select(static item => item.Id + ':' + item.BodyRevision)
                .Concat(files.Select(static item => item.AttachmentId + ':' + item.Sha256)));
        return ContentRevision.Sha256Hex(raw);
    }
}
