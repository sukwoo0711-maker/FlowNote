using System.IO;
using FlowNote.Core.Models;
using FlowNote.Core.Rules;

namespace FlowNote.Desktop.ViewModels;

public sealed class CapsuleRecentRow
{
    public required string Id { get; init; }

    public required DateOnly LocalDate { get; init; }

    public required string TimeLabel { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    public required string KindLabel { get; init; }

    public required bool IsCompleted { get; init; }

    public required bool IsEvent { get; init; }

    public string? ImagePath { get; init; }

    public string? FileName { get; init; }

    public string NodeKind { get; init; } = "note";

    public string StickyText => NoteDisplayRules.StickyText(
        Body,
        Title,
        !string.IsNullOrWhiteSpace(ImagePath) || !string.IsNullOrWhiteSpace(FileName));

    public bool HasStickyText => !string.IsNullOrWhiteSpace(StickyText);

    public static CapsuleRecentRow From(TimelineEntry entry, AppSession session)
    {
        var zone = session.Database.DisplayTimeZone;
        var local = TimeZoneInfo.ConvertTime(entry.OccurredAtUtc, zone.TimeZone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone.TimeZone).DateTime);
        var localDate = zone.GetLocalDate(entry.OccurredAtUtc);
        var attachments = session.Database.Entries.ListAttachments(entry.Id);
        string? imagePath = null;
        string? fileName = null;
        foreach (var attachment in attachments)
        {
            var full = session.Database.Attachments.ResolveFullPath(attachment);
            if (AttachmentRules.IsImage(attachment.MediaType) && imagePath is null && File.Exists(full))
            {
                imagePath = full;
            }
            else if (fileName is null)
            {
                fileName = attachment.OriginalName;
            }
        }

        var title = !string.IsNullOrWhiteSpace(entry.TitleSnapshot)
            ? entry.TitleSnapshot
            : !string.IsNullOrWhiteSpace(entry.Body)
                ? FirstLineOf(entry.Body)
                : fileName ?? TimelineDisplayRules.KindLabel(entry.Kind);

        var time = local.ToString("HH:mm");
        if (localDate == today.AddDays(-1))
        {
            time = "어제 " + time;
        }
        else if (localDate != today)
        {
            time = localDate.ToString("M/d ") + time;
        }

        return new CapsuleRecentRow
        {
            Id = entry.Id,
            LocalDate = localDate,
            TimeLabel = time,
            Title = title,
            Body = entry.Body ?? "",
            KindLabel = TimelineDisplayRules.KindLabel(entry.Kind),
            IsCompleted = entry.Kind == EntryKind.TaskCompleted,
            IsEvent = entry.Kind != EntryKind.Note,
            ImagePath = imagePath,
            FileName = fileName,
            NodeKind = TimelineDisplayRules.NodeKind(entry.Kind)
        };
    }

    public static string FirstLineOf(string body)
    {
        var line = (body ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')[0].Trim();
        return line.Length <= 80 ? line : line[..80];
    }
}
