using FlowNote.Core.Models;
using FlowNote.Core.Time;

namespace FlowNote.Core.Rules;

public static class DayAggregator
{
    public static DaySummary Summarize(
        IReadOnlyList<TimelineEntry> entriesOnDate,
        IReadOnlyList<TimelineEntry> lifecycleHistoryThroughEnd,
        DateOnly selectedDate,
        DisplayTimeZone timeZone)
    {
        var visible = entriesOnDate.Where(static entry => entry.DeletedAtUtc is null).ToList();
        var entryCount = visible.Count;
        var completed = CountCompletedWorkItems(lifecycleHistoryThroughEnd, selectedDate, timeZone);
        return new DaySummary(entryCount, completed, AttachmentCount: 0);
    }

    public static int CountCompletedWorkItems(
        IReadOnlyList<TimelineEntry> lifecycleHistoryThroughEnd,
        DateOnly selectedDate,
        DisplayTimeZone timeZone)
    {
        var latestByWorkItem = lifecycleHistoryThroughEnd
            .Where(static entry => entry.DeletedAtUtc is null)
            .Where(static entry => entry.WorkItemId is not null)
            .Where(static entry => entry.Kind is EntryKind.TaskCreated or EntryKind.TaskCompleted or EntryKind.TaskReopened or EntryKind.TaskCancelled)
            .GroupBy(static entry => entry.WorkItemId!)
            .Select(static group => group
                .OrderBy(static entry => entry.OccurredAtUtc)
                .ThenBy(static entry => entry.Seq)
                .Last());

        var count = 0;
        foreach (var latest in latestByWorkItem)
        {
            if (latest.Kind == EntryKind.TaskCompleted && timeZone.GetLocalDate(latest.OccurredAtUtc) == selectedDate)
            {
                count++;
            }
        }

        return count;
    }
}
