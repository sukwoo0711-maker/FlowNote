using FlowNote.Core.Errors;
using FlowNote.Core.Models;
using FlowNote.Core.Rules;
using FlowNote.Core.Time;
using FlowNote.Infrastructure.Persistence;

namespace FlowNote.Infrastructure.Tests;

public sealed class WorkItemPersistenceTests
{
    [Fact]
    public async Task Create_stores_open_work_item_and_created_event()
    {
        using var temp = new TempDatabase();
        var result = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest
        {
            RequestId = "create-1",
            Title = "취소 후 재시작 오류 재현"
        });

        Assert.Equal(WorkItemStatus.Open, result.WorkItem.Status);
        Assert.Equal(EntryKind.TaskCreated, result.Event?.Kind);
        var listed = await temp.Database.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14));
        Assert.Contains(listed, item => item.Kind == EntryKind.TaskCreated && item.WorkItemId == result.WorkItem.Id);
    }

    [Fact]
    public async Task Complete_is_atomic_with_timeline_event()
    {
        using var temp = new TempDatabase();
        var created = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "c", Title = "할 일" });
        var completed = await temp.Database.WorkItems.CompleteAsync(new WorkItemCommandRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "complete-1",
            ExpectedVersion = created.WorkItem.Version
        });

        Assert.Equal(WorkItemStatus.Completed, completed.WorkItem.Status);
        Assert.Equal(EntryKind.TaskCompleted, completed.Event?.Kind);
        Assert.Equal(created.WorkItem.Id, completed.Event?.WorkItemId);
        Assert.Empty(await temp.Database.WorkItems.ListByStatusAsync(WorkItemStatus.Open));
        Assert.Single(await temp.Database.WorkItems.ListByStatusAsync(WorkItemStatus.Completed));
    }

    [Fact]
    public async Task Same_complete_request_id_does_not_duplicate_event()
    {
        using var temp = new TempDatabase();
        var created = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "c", Title = "할 일" });
        var request = new WorkItemCommandRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "dup-complete",
            ExpectedVersion = created.WorkItem.Version
        };
        var first = await temp.Database.WorkItems.CompleteAsync(request);
        var second = await temp.Database.WorkItems.CompleteAsync(request);
        var day = await temp.Database.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14));

        Assert.Equal(first.Event?.Id, second.Event?.Id);
        Assert.Equal(1, day.Count(static item => item.Kind == EntryKind.TaskCompleted));
    }

    [Fact]
    public async Task Reopen_keeps_past_completion_event()
    {
        using var temp = new TempDatabase();
        var created = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "c", Title = "할 일" });
        var completed = await temp.Database.WorkItems.CompleteAsync(new WorkItemCommandRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "done",
            ExpectedVersion = created.WorkItem.Version
        });
        var reopened = await temp.Database.WorkItems.ReopenAsync(new WorkItemCommandRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "undo",
            ExpectedVersion = completed.WorkItem.Version
        });

        Assert.Equal(WorkItemStatus.Open, reopened.WorkItem.Status);
        Assert.Null(reopened.WorkItem.CompletedAtUtc);
        Assert.Equal(EntryKind.TaskReopened, reopened.Event?.Kind);
        Assert.Equal(completed.Event?.Id, reopened.Event?.ReversesEntryId);
        var day = await temp.Database.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14));
        Assert.Contains(day, static item => item.Kind == EntryKind.TaskCompleted);
        Assert.Contains(day, static item => item.Kind == EntryKind.TaskReopened);
    }

    [Fact]
    public async Task Same_day_recomplete_counts_as_one_completed_work_item()
    {
        using var temp = new TempDatabase();
        var created = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "c", Title = "할 일" });
        var completed = await temp.Database.WorkItems.CompleteAsync(new WorkItemCommandRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "done1",
            ExpectedVersion = created.WorkItem.Version
        });
        var reopened = await temp.Database.WorkItems.ReopenAsync(new WorkItemCommandRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "undo",
            ExpectedVersion = completed.WorkItem.Version
        });
        await temp.Database.WorkItems.CompleteAsync(new WorkItemCommandRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "done2",
            ExpectedVersion = reopened.WorkItem.Version
        });

        var selected = new DateOnly(2026, 9, 14);
        var dayEntries = await temp.Database.Entries.ListForLocalDateAsync(selected);
        var throughEnd = temp.Database.Entries.ListLifecycleThrough(temp.TimeZone.GetUtcRange(selected).ExclusiveEndUtc);
        var completedCount = DayAggregator.CountCompletedWorkItems(throughEnd, selected, temp.TimeZone);
        Assert.Equal(1, completedCount);
        Assert.Equal(2, dayEntries.Count(static item => item.Kind == EntryKind.TaskCompleted));
    }

    [Fact]
    public async Task Next_day_reopen_keeps_previous_day_completion_as_of_that_evening()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 14, 1, 24, 0, TimeSpan.Zero));
        using var temp = new TempDatabase(clock);
        var created = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "c", Title = "할 일" });
        await temp.Database.WorkItems.CompleteAsync(new WorkItemCommandRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "done",
            ExpectedVersion = created.WorkItem.Version
        });

        var previous = new DateOnly(2026, 9, 14);
        var throughPrevious = temp.Database.Entries.ListLifecycleThrough(temp.TimeZone.GetUtcRange(previous).ExclusiveEndUtc);
        Assert.Equal(1, DayAggregator.CountCompletedWorkItems(throughPrevious, previous, temp.TimeZone));

        clock.UtcNow = new DateTimeOffset(2026, 9, 14, 16, 0, 0, TimeSpan.Zero);
        var current = await temp.Database.WorkItems.GetAsync(created.WorkItem.Id);
        await temp.Database.WorkItems.ReopenAsync(new WorkItemCommandRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "next-day-reopen",
            ExpectedVersion = current!.Version
        });

        var throughPreviousAgain = temp.Database.Entries.ListLifecycleThrough(temp.TimeZone.GetUtcRange(previous).ExclusiveEndUtc);
        Assert.Equal(1, DayAggregator.CountCompletedWorkItems(throughPreviousAgain, previous, temp.TimeZone));
        var today = await temp.Database.WorkItems.GetAsync(created.WorkItem.Id);
        Assert.Equal(WorkItemStatus.Open, today!.Status);
    }

    [Fact]
    public async Task Cancel_is_not_a_completion()
    {
        using var temp = new TempDatabase();
        var created = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "c", Title = "하지 않기로 한 일" });
        await temp.Database.WorkItems.CancelAsync(new WorkItemCommandRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "cancel",
            ExpectedVersion = created.WorkItem.Version
        });

        Assert.Empty(await temp.Database.WorkItems.ListByStatusAsync(WorkItemStatus.Open));
        Assert.Single(await temp.Database.WorkItems.ListByStatusAsync(WorkItemStatus.Cancelled));
        var selected = new DateOnly(2026, 9, 14);
        var throughEnd = temp.Database.Entries.ListLifecycleThrough(temp.TimeZone.GetUtcRange(selected).ExclusiveEndUtc);
        Assert.Equal(0, DayAggregator.CountCompletedWorkItems(throughEnd, selected, temp.TimeZone));
    }

    [Fact]
    public async Task Stale_version_does_not_complete()
    {
        using var temp = new TempDatabase();
        var created = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "c", Title = "할 일" });
        await Assert.ThrowsAsync<VersionConflictException>(() => temp.Database.WorkItems.CompleteAsync(new WorkItemCommandRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "stale",
            ExpectedVersion = created.WorkItem.Version - 1
        }));
        Assert.Equal(WorkItemStatus.Open, (await temp.Database.WorkItems.GetAsync(created.WorkItem.Id))!.Status);
    }

    [Fact]
    public async Task Draft_is_not_a_timeline_entry()
    {
        using var temp = new TempDatabase();
        temp.Database.Drafts.Save("floating-note", "note", "초안 본문", null, null, null);
        Assert.Empty(await temp.Database.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14)));

        temp.Reopen();
        var draft = temp.Database.Drafts.Load("floating-note");
        Assert.Equal("초안 본문", draft?.Body);
    }
}
