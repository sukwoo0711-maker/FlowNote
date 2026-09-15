using FlowNote.Core.Models;
using FlowNote.Core.Rules;

namespace FlowNote.Core.Tests;

public sealed class WorkItemStateMachineTests
{
    [Fact]
    public void Complete_from_open_creates_completed_event()
    {
        var result = WorkItemStateMachine.Decide(WorkItemStatus.Open, WorkItemCommand.Complete);
        Assert.False(result.NoOp);
        Assert.Equal(WorkItemStatus.Completed, result.NextStatus);
        Assert.Equal(EntryKind.TaskCompleted, result.EventKind);
    }

    [Fact]
    public void Duplicate_complete_is_noop()
    {
        var result = WorkItemStateMachine.Decide(WorkItemStatus.Completed, WorkItemCommand.Complete);
        Assert.True(result.NoOp);
        Assert.Null(result.EventKind);
    }

    [Fact]
    public void Completed_cannot_cancel_without_reopen()
    {
        Assert.Throws<InvalidOperationException>(() =>
            WorkItemStateMachine.Decide(WorkItemStatus.Completed, WorkItemCommand.Cancel));
    }

    [Fact]
    public void Reopen_from_completed_creates_reopened_event()
    {
        var result = WorkItemStateMachine.Decide(WorkItemStatus.Completed, WorkItemCommand.Reopen);
        Assert.Equal(WorkItemStatus.Open, result.NextStatus);
        Assert.Equal(EntryKind.TaskReopened, result.EventKind);
    }
}
