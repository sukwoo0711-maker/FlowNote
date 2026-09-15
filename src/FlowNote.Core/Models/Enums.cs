namespace FlowNote.Core.Models;

public enum EntryKind
{
    Note,
    TaskCreated,
    TaskCompleted,
    TaskReopened,
    TaskCancelled
}

public enum OccurredTimeSource
{
    Recorded,
    User
}

public enum WorkItemStatus
{
    Open,
    Completed,
    Cancelled
}

public static class EntryKindText
{
    public static string ToStorage(EntryKind kind)
    {
        return kind switch
        {
            EntryKind.Note => "note",
            EntryKind.TaskCreated => "task_created",
            EntryKind.TaskCompleted => "task_completed",
            EntryKind.TaskReopened => "task_reopened",
            EntryKind.TaskCancelled => "task_cancelled",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public static EntryKind Parse(string value)
    {
        return value switch
        {
            "note" => EntryKind.Note,
            "task_created" => EntryKind.TaskCreated,
            "task_completed" => EntryKind.TaskCompleted,
            "task_reopened" => EntryKind.TaskReopened,
            "task_cancelled" => EntryKind.TaskCancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}

public static class WorkItemStatusText
{
    public static string ToStorage(WorkItemStatus status)
    {
        return status switch
        {
            WorkItemStatus.Open => "open",
            WorkItemStatus.Completed => "completed",
            WorkItemStatus.Cancelled => "cancelled",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    public static WorkItemStatus Parse(string value)
    {
        return value switch
        {
            "open" => WorkItemStatus.Open,
            "completed" => WorkItemStatus.Completed,
            "cancelled" => WorkItemStatus.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}
