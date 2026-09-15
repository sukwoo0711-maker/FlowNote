using FlowNote.Core.Models;

namespace FlowNote.Core.Rules;

public static class WorkItemStateMachine
{
    public static WorkItemTransition Decide(WorkItemStatus current, WorkItemCommand command)
    {
        return (current, command) switch
        {
            (_, WorkItemCommand.Create) => new WorkItemTransition(WorkItemStatus.Open, EntryKind.TaskCreated, NoOp: false),
            (WorkItemStatus.Open, WorkItemCommand.Complete) => new WorkItemTransition(WorkItemStatus.Completed, EntryKind.TaskCompleted, NoOp: false),
            (WorkItemStatus.Completed, WorkItemCommand.Reopen) => new WorkItemTransition(WorkItemStatus.Open, EntryKind.TaskReopened, NoOp: false),
            (WorkItemStatus.Cancelled, WorkItemCommand.Reopen) => new WorkItemTransition(WorkItemStatus.Open, EntryKind.TaskReopened, NoOp: false),
            (WorkItemStatus.Open, WorkItemCommand.Cancel) => new WorkItemTransition(WorkItemStatus.Cancelled, EntryKind.TaskCancelled, NoOp: false),
            (WorkItemStatus.Completed, WorkItemCommand.Complete) => new WorkItemTransition(WorkItemStatus.Completed, null, NoOp: true),
            (WorkItemStatus.Open, WorkItemCommand.Reopen) => new WorkItemTransition(WorkItemStatus.Open, null, NoOp: true),
            (WorkItemStatus.Cancelled, WorkItemCommand.Cancel) => new WorkItemTransition(WorkItemStatus.Cancelled, null, NoOp: true),
            (WorkItemStatus.Completed, WorkItemCommand.Cancel) => throw new InvalidOperationException("완료된 업무는 먼저 다시 열어야 취소할 수 있습니다."),
            (WorkItemStatus.Cancelled, WorkItemCommand.Complete) => throw new InvalidOperationException("취소된 업무는 먼저 다시 열어야 완료할 수 있습니다."),
            _ => throw new InvalidOperationException("허용되지 않은 상태 변경입니다.")
        };
    }
}

public enum WorkItemCommand
{
    Create,
    Complete,
    Reopen,
    Cancel
}

public readonly record struct WorkItemTransition(WorkItemStatus NextStatus, EntryKind? EventKind, bool NoOp);
