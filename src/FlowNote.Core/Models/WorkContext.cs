namespace FlowNote.Core.Models;

public sealed class WorkContextSnapshot
{
    public required WorkItem WorkItem { get; init; }
    public required string StatusLabel { get; init; }
    public TimelineEntry? LatestNote { get; init; }
    public IReadOnlyList<WorkContextAttachment> RelatedAttachments { get; init; } = [];
    public IReadOnlyList<TimelineEntry> RecentEntries { get; init; } = [];
    public string? NextActionText { get; init; }
    public bool NextActionSourceUnavailable { get; init; }
    public string? LastClearedNextAction { get; init; }
    public required long RequestGeneration { get; init; }
}

public sealed class WorkContextAttachment
{
    public required StoredAttachment Attachment { get; init; }
    public required string SourceEntryId { get; init; }
}

public sealed class ContinueWorkItem
{
    public required WorkItem WorkItem { get; init; }
    public required DateTimeOffset ContentChangedAtUtc { get; init; }
}

public enum DraftWorkLinkDecision
{
    ApplyDirectly,
    SameWork,
    Confirm
}
