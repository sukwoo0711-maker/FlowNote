using FlowNote.Core.Models;

namespace FlowNote.Core.Rules;

public static class WorkAsOf
{
    public static WorkItemStatus? StatusAsOf(IEnumerable<TimelineEntry> lifecycle, string workItemId)
    {
        WorkItemStatus? status = null;
        foreach (var entry in lifecycle
                     .Where(item => item.WorkItemId == workItemId)
                     .OrderBy(static item => item.OccurredAtUtc)
                     .ThenBy(static item => item.Seq))
        {
            status = entry.Kind switch
            {
                EntryKind.TaskCreated => WorkItemStatus.Open,
                EntryKind.TaskCompleted => WorkItemStatus.Completed,
                EntryKind.TaskReopened => WorkItemStatus.Open,
                EntryKind.TaskCancelled => WorkItemStatus.Cancelled,
                _ => status
            };
        }

        return status;
    }
}
