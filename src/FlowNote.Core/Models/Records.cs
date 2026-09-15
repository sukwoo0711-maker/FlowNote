using FlowNote.Core.Time;

namespace FlowNote.Core.Models;

public sealed class TimelineEntry
{
    public required long Seq { get; init; }
    public required string Id { get; init; }
    public required string RequestId { get; init; }
    public required EntryKind Kind { get; init; }
    public string? WorkItemId { get; init; }
    public string? ProjectId { get; init; }
    public string? TitleSnapshot { get; init; }
    public required string Body { get; init; }
    public required DateTimeOffset RecordedAtUtc { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
    public required OccurredTimeSource OccurredTimeSource { get; init; }
    public required int RecordedOffsetMinutes { get; init; }
    public string? IssueKey { get; init; }
    public string? IssueUrl { get; init; }
    public string? ReversesEntryId { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    public DateTimeOffset? DeletedAtUtc { get; init; }

    public DateOnly GetOccurredLocalDate(DisplayTimeZone timeZone) => timeZone.GetLocalDate(OccurredAtUtc);
}

public sealed class WorkItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required WorkItemStatus Status { get; init; }
    public string? ProjectId { get; init; }
    public string? IssueKey { get; init; }
    public string? IssueUrl { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public required int Version { get; init; }
    public string? NextActionText { get; init; }
    public string? NextActionSourceEntryId { get; init; }
    public string? NextActionSourceRevision { get; init; }
    public DateTimeOffset? NextActionUpdatedAtUtc { get; init; }
}

public sealed class SaveNoteRequest
{
    public required string RequestId { get; init; }
    public string Body { get; init; } = "";
    public string? Title { get; init; }
    public DateTimeOffset? OccurredAtUtc { get; init; }
    public string? WorkItemId { get; init; }
    public string? ProjectId { get; init; }
    public string? IssueKey { get; init; }
    public string? IssueUrl { get; init; }
    public bool HasAttachments { get; init; }
}

public sealed class CreateWorkItemRequest
{
    public required string RequestId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? ProjectId { get; init; }
    public string? IssueKey { get; init; }
    public string? IssueUrl { get; init; }
}

public sealed class WorkItemCommandRequest
{
    public required string WorkItemId { get; init; }
    public required string RequestId { get; init; }
    public required int ExpectedVersion { get; init; }
}

public sealed record WorkItemMutationResult(WorkItem WorkItem, TimelineEntry? Event, bool AlreadyApplied);

public sealed record DaySummary(int EntryCount, int CompletedWorkItemCount, int AttachmentCount);
