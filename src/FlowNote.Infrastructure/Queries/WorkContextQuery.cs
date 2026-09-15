using FlowNote.Core.Models;
using FlowNote.Core.Rules;
using FlowNote.Infrastructure.Persistence;

namespace FlowNote.Infrastructure.Queries;

public sealed class WorkContextQuery
{
    private readonly FlowNoteDatabase _database;

    public WorkContextQuery(FlowNoteDatabase database)
    {
        _database = database;
    }

    public WorkContextSnapshot Get(string workItemId, long requestGeneration)
    {
        var item = _database.WorkItems.GetAsync(workItemId).GetAwaiter().GetResult()
            ?? throw new FlowNote.Core.Errors.ValidationException("할 일을 찾을 수 없습니다.");
        var notes = _database.Entries.ListForWorkItem(workItemId, notesOnly: true);
        var latest = notes.FirstOrDefault();
        var recent = notes.Take(3).ToList();
        var attachments = DistinctRecentAttachments(workItemId, 3);
        var history = _database.WorkItems.ListNextActionHistory(workItemId);
        var sourceUnavailable = SourceUnavailable(item);
        return new WorkContextSnapshot
        {
            WorkItem = item,
            StatusLabel = WorkChipText.StatusLabel(item.Status),
            LatestNote = latest,
            RelatedAttachments = attachments,
            RecentEntries = recent,
            NextActionText = item.NextActionText,
            NextActionSourceUnavailable = sourceUnavailable,
            LastClearedNextAction = NextActionRules.LastClearedText(history),
            RequestGeneration = requestGeneration
        };
    }

    public IReadOnlyList<ContinueWorkItem> ListContinue()
    {
        var open = _database.WorkItems.ListByStatusAsync(WorkItemStatus.Open).GetAwaiter().GetResult();
        var rows = new List<ContinueWorkItem>(open.Count);
        foreach (var item in open)
        {
            var notes = _database.Entries.ListForWorkItem(item.Id, notesOnly: true);
            var latestNote = notes.FirstOrDefault()?.OccurredAtUtc;
            var changed = item.CreatedAtUtc;
            if (latestNote is { } noteTime && noteTime > changed)
            {
                changed = noteTime;
            }

            if (item.NextActionUpdatedAtUtc is { } actionTime && actionTime > changed)
            {
                changed = actionTime;
            }

            rows.Add(new ContinueWorkItem { WorkItem = item, ContentChangedAtUtc = changed });
        }

        return ContinueWorkSorter.Sort(
            rows,
            static row => !string.IsNullOrWhiteSpace(row.WorkItem.NextActionText),
            static row => row.ContentChangedAtUtc,
            static row => row.WorkItem.Id);
    }

    private IReadOnlyList<WorkContextAttachment> DistinctRecentAttachments(string workItemId, int take)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<WorkContextAttachment>();
        foreach (var entry in _database.Entries.ListForWorkItem(workItemId, notesOnly: false))
        {
            foreach (var attachment in _database.Entries.ListAttachments(entry.Id))
            {
                if (!seen.Add(attachment.Id))
                {
                    continue;
                }

                result.Add(new WorkContextAttachment { Attachment = attachment, SourceEntryId = entry.Id });
                if (result.Count >= take)
                {
                    return result;
                }
            }
        }

        return result;
    }

    private bool SourceUnavailable(WorkItem item)
    {
        if (string.IsNullOrEmpty(item.NextActionSourceEntryId))
        {
            return false;
        }

        var source = _database.Entries.GetByIdAsync(item.NextActionSourceEntryId).GetAwaiter().GetResult();
        if (source is null || source.DeletedAtUtc is not null)
        {
            return true;
        }

        if (!string.Equals(source.WorkItemId, item.Id, StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(item.NextActionSourceRevision)
            && !string.Equals(item.NextActionSourceRevision, ContentRevision.Sha256Hex(source.Body), StringComparison.Ordinal))
        {
            return false;
        }

        return false;
    }
}
