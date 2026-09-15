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

    public required string KindLabel { get; init; }

    public required bool IsCompleted { get; init; }

    public required bool IsEvent { get; init; }

    public string? ImagePath { get; init; }

    public string NodeKind { get; init; } = "note";

    public static CapsuleRecentRow From(TimelineEntry entry, AppSession session)
    {
        var local = TimeZoneInfo.ConvertTime(entry.OccurredAtUtc, session.Database.DisplayTimeZone.TimeZone);
        var attachments = session.Database.Entries.ListAttachments(entry.Id);
        string? imagePath = null;
        foreach (var attachment in attachments)
        {
            if (!AttachmentRules.IsImage(attachment.MediaType))
            {
                continue;
            }

            var full = session.Database.Attachments.ResolveFullPath(attachment);
            if (File.Exists(full))
            {
                imagePath = full;
                break;
            }
        }

        var title = !string.IsNullOrWhiteSpace(entry.TitleSnapshot)
            ? entry.TitleSnapshot
            : !string.IsNullOrWhiteSpace(entry.Body)
                ? FirstLine(entry.Body)
                : attachments.Count > 0
                    ? attachments[0].OriginalName
                    : TimelineDisplayRules.KindLabel(entry.Kind);

        return new CapsuleRecentRow
        {
            Id = entry.Id,
            LocalDate = session.Database.DisplayTimeZone.GetLocalDate(entry.OccurredAtUtc),
            TimeLabel = local.ToString("HH:mm"),
            Title = title,
            KindLabel = TimelineDisplayRules.KindLabel(entry.Kind),
            IsCompleted = entry.Kind == EntryKind.TaskCompleted,
            IsEvent = entry.Kind != EntryKind.Note,
            ImagePath = imagePath,
            NodeKind = TimelineDisplayRules.NodeKind(entry.Kind)
        };
    }

    private static string FirstLine(string body)
    {
        var line = body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')[0].Trim();
        return line.Length <= 80 ? line : line[..80];
    }
}
