using FlowNote.Core.Models;
using FlowNote.Core.Rules;
using FlowNote.Core.Time;

namespace FlowNote.Core.Tests;

public sealed class DayAggregatorTests
{
    private readonly DisplayTimeZone _korea = DisplayTimeZone.Korea();

    [Fact]
    public void Completion_then_reopen_then_complete_same_day_counts_one_work_item()
    {
        var day = new DateOnly(2026, 9, 14);
        var history = new[]
        {
            Event("w1", EntryKind.TaskCreated, "2026-09-14T01:00:00.000Z", 1),
            Event("w1", EntryKind.TaskCompleted, "2026-09-14T01:24:00.000Z", 2),
            Event("w1", EntryKind.TaskReopened, "2026-09-14T01:35:00.000Z", 3),
            Event("w1", EntryKind.TaskCompleted, "2026-09-14T01:55:00.000Z", 4)
        };

        Assert.Equal(1, DayAggregator.CountCompletedWorkItems(history, day, _korea));
    }

    [Fact]
    public void Next_day_reopen_does_not_change_previous_day_completion_count()
    {
        var previousDay = new DateOnly(2026, 9, 14);
        var historyThroughPreviousDayEnd = new[]
        {
            Event("w1", EntryKind.TaskCreated, "2026-09-14T01:00:00.000Z", 1),
            Event("w1", EntryKind.TaskCompleted, "2026-09-14T01:24:00.000Z", 2)
        };
        var historyThroughNextDay = new[]
        {
            historyThroughPreviousDayEnd[0],
            historyThroughPreviousDayEnd[1],
            Event("w1", EntryKind.TaskReopened, "2026-09-14T16:00:00.000Z", 3)
        };

        Assert.Equal(1, DayAggregator.CountCompletedWorkItems(historyThroughPreviousDayEnd, previousDay, _korea));
        Assert.Equal(0, DayAggregator.CountCompletedWorkItems(historyThroughNextDay, new DateOnly(2026, 9, 15), _korea));
    }

    [Fact]
    public void Two_notes_hours_apart_do_not_become_work_duration()
    {
        var day = new DateOnly(2026, 9, 14);
        var notes = new[]
        {
            Note("2026-09-14T00:00:00.000Z", 1),
            Note("2026-09-14T03:00:00.000Z", 2)
        };

        var summary = DayAggregator.Summarize(notes, Array.Empty<TimelineEntry>(), day, _korea);
        Assert.Equal(2, summary.EntryCount);
        Assert.Equal(0, summary.CompletedWorkItemCount);
        Assert.Equal(0, summary.AttachmentCount);
        Assert.Null(summary.GetType().GetProperty("WorkDuration"));
        Assert.Null(summary.GetType().GetProperty("FocusMinutes"));
    }

    [Fact]
    public void Cancelled_work_is_not_a_completion()
    {
        var day = new DateOnly(2026, 9, 14);
        var history = new[]
        {
            Event("w1", EntryKind.TaskCreated, "2026-09-14T01:00:00.000Z", 1),
            Event("w1", EntryKind.TaskCancelled, "2026-09-14T02:00:00.000Z", 2)
        };
        Assert.Equal(0, DayAggregator.CountCompletedWorkItems(history, day, _korea));
    }

    private static TimelineEntry Event(string workItemId, EntryKind kind, string occurred, long seq)
    {
        var at = UtcInstant.Parse(occurred);
        return new TimelineEntry
        {
            Seq = seq,
            Id = Guid.NewGuid().ToString("D"),
            RequestId = Guid.NewGuid().ToString("D"),
            Kind = kind,
            WorkItemId = workItemId,
            TitleSnapshot = "업무",
            Body = "",
            RecordedAtUtc = at,
            OccurredAtUtc = at,
            OccurredTimeSource = OccurredTimeSource.Recorded,
            RecordedOffsetMinutes = 540
        };
    }

    private static TimelineEntry Note(string occurred, long seq)
    {
        var at = UtcInstant.Parse(occurred);
        return new TimelineEntry
        {
            Seq = seq,
            Id = Guid.NewGuid().ToString("D"),
            RequestId = Guid.NewGuid().ToString("D"),
            Kind = EntryKind.Note,
            Body = "메모",
            RecordedAtUtc = at,
            OccurredAtUtc = at,
            OccurredTimeSource = OccurredTimeSource.Recorded,
            RecordedOffsetMinutes = 540
        };
    }
}
