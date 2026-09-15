using FlowNote.Core.Errors;
using FlowNote.Core.Models;
using FlowNote.Core.Rules;
using FlowNote.Core.Time;

namespace FlowNote.Infrastructure.Tests;

public sealed class NextActionPersistenceTests
{
    [Fact]
    public async Task Set_update_clear_keeps_one_current_and_history()
    {
        using var temp = new TempDatabase();
        var created = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "c", Title = "할 일" });
        var first = await temp.Database.WorkItems.SetNextActionAsync(new NextActionRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "na-1",
            ExpectedVersion = created.WorkItem.Version,
            Text = "펌프 OFF를 확인한다",
            Reason = NextActionChangeReason.Set
        });
        var second = await temp.Database.WorkItems.SetNextActionAsync(new NextActionRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "na-2",
            ExpectedVersion = first.WorkItem.Version,
            Text = "초기화 순서를 비교한다",
            Reason = NextActionChangeReason.Update
        });
        await temp.Database.WorkItems.SetNextActionAsync(new NextActionRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "na-3",
            ExpectedVersion = second.WorkItem.Version,
            Reason = NextActionChangeReason.Clear
        });

        var current = await temp.Database.WorkItems.GetAsync(created.WorkItem.Id);
        Assert.Null(current!.NextActionText);
        Assert.Equal(3, temp.Database.WorkItems.ListNextActionHistory(created.WorkItem.Id).Count);
    }

    [Fact]
    public async Task Same_request_does_not_duplicate_history()
    {
        using var temp = new TempDatabase();
        var created = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "c", Title = "할 일" });
        var request = new NextActionRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "same-na",
            ExpectedVersion = created.WorkItem.Version,
            Text = "한 줄",
            Reason = NextActionChangeReason.Set
        };
        await temp.Database.WorkItems.SetNextActionAsync(request);
        await temp.Database.WorkItems.SetNextActionAsync(request);
        Assert.Single(temp.Database.WorkItems.ListNextActionHistory(created.WorkItem.Id));
    }

    [Fact]
    public async Task Complete_clears_next_action_and_keeps_history()
    {
        using var temp = new TempDatabase();
        var created = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "c", Title = "할 일" });
        var set = await temp.Database.WorkItems.SetNextActionAsync(new NextActionRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "na",
            ExpectedVersion = created.WorkItem.Version,
            Text = "남아 있던 행동",
            Reason = NextActionChangeReason.Set
        });
        var completed = await temp.Database.WorkItems.CompleteAsync(new WorkItemCommandRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "done",
            ExpectedVersion = set.WorkItem.Version
        });
        Assert.Null(completed.WorkItem.NextActionText);
        Assert.Contains(temp.Database.WorkItems.ListNextActionHistory(created.WorkItem.Id),
            static item => item.Reason == NextActionChangeReason.Completion && item.PreviousText == "남아 있던 행동");

        var reopened = await temp.Database.WorkItems.ReopenAsync(new WorkItemCommandRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "reopen",
            ExpectedVersion = completed.WorkItem.Version
        });
        Assert.Null(reopened.WorkItem.NextActionText);

        await temp.Database.WorkItems.SetNextActionAsync(new NextActionRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "restore",
            ExpectedVersion = reopened.WorkItem.Version,
            Text = "남아 있던 행동",
            Reason = NextActionChangeReason.Restore
        });
        Assert.Equal("남아 있던 행동", (await temp.Database.WorkItems.GetAsync(created.WorkItem.Id))!.NextActionText);
    }

    [Fact]
    public async Task Source_edit_does_not_change_next_action()
    {
        using var temp = new TempDatabase();
        var created = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "c", Title = "할 일" });
        var note = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "n",
            Body = "원문",
            WorkItemId = created.WorkItem.Id
        });
        await temp.Database.WorkItems.SetNextActionAsync(new NextActionRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "na",
            ExpectedVersion = created.WorkItem.Version,
            Text = "원문",
            SourceEntryId = note.Id,
            SourceRevision = ContentRevision.Sha256Hex(note.Body),
            Reason = NextActionChangeReason.Set
        });
        await temp.Database.Entries.UpdateNoteAsync(new UpdateNoteRequest(note.Id, "바뀐 원문"));
        var current = await temp.Database.WorkItems.GetAsync(created.WorkItem.Id);
        Assert.Equal("원문", current!.NextActionText);
        Assert.Equal(note.Id, current.NextActionSourceEntryId);
    }

    [Fact]
    public async Task Past_as_of_does_not_use_current_value()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 14, 0, 25, 0, TimeSpan.Zero));
        using var temp = new TempDatabase(clock);
        var created = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "c", Title = "할 일" });
        await temp.Database.WorkItems.SetNextActionAsync(new NextActionRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "na1",
            ExpectedVersion = created.WorkItem.Version,
            Text = "14일 행동",
            Reason = NextActionChangeReason.Set
        });
        clock.UtcNow = new DateTimeOffset(2026, 9, 15, 0, 20, 0, TimeSpan.Zero);
        var current = await temp.Database.WorkItems.GetAsync(created.WorkItem.Id);
        await temp.Database.WorkItems.SetNextActionAsync(new NextActionRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "na2",
            ExpectedVersion = current!.Version,
            Text = "15일 행동",
            Reason = NextActionChangeReason.Update
        });

        var asOf14 = temp.Database.WorkItems.GetNextActionAsOf(
            created.WorkItem.Id,
            temp.TimeZone.GetUtcRange(new DateOnly(2026, 9, 14)).ExclusiveEndUtc);
        Assert.Equal("14일 행동", asOf14.Text);
        var beforeHistory = temp.Database.WorkItems.GetNextActionAsOf(
            created.WorkItem.Id,
            new DateTimeOffset(2026, 9, 13, 15, 0, 0, TimeSpan.Zero));
        Assert.False(beforeHistory.Recorded);
    }

    [Fact]
    public async Task Stale_version_does_not_overwrite()
    {
        using var temp = new TempDatabase();
        var created = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "c", Title = "할 일" });
        await temp.Database.WorkItems.SetNextActionAsync(new NextActionRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "na1",
            ExpectedVersion = created.WorkItem.Version,
            Text = "최신",
            Reason = NextActionChangeReason.Set
        });
        await Assert.ThrowsAsync<VersionConflictException>(() => temp.Database.WorkItems.SetNextActionAsync(new NextActionRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "stale",
            ExpectedVersion = created.WorkItem.Version,
            Text = "덮어쓰기",
            Reason = NextActionChangeReason.Update
        }));
        Assert.Equal("최신", (await temp.Database.WorkItems.GetAsync(created.WorkItem.Id))!.NextActionText);
    }

    [Fact]
    public async Task Completed_work_rejects_new_next_action()
    {
        using var temp = new TempDatabase();
        var created = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "c", Title = "할 일" });
        var completed = await temp.Database.WorkItems.CompleteAsync(new WorkItemCommandRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "done",
            ExpectedVersion = created.WorkItem.Version
        });
        await Assert.ThrowsAsync<ValidationException>(() => temp.Database.WorkItems.SetNextActionAsync(new NextActionRequest
        {
            WorkItemId = created.WorkItem.Id,
            RequestId = "na",
            ExpectedVersion = completed.WorkItem.Version,
            Text = "안 됨",
            Reason = NextActionChangeReason.Set
        }));
    }
}
