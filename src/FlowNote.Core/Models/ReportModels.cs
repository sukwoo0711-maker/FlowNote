namespace FlowNote.Core.Models;

public enum ReportCandidateKind
{
    Note,
    LifecycleCompleted,
    LifecycleOther,
    NextAction
}

public sealed class ReportFileCandidate
{
    public required string AttachmentId { get; init; }
    public required string EntryId { get; init; }
    public required string OriginalName { get; init; }
    public required string Sha256 { get; init; }
    public required long ByteSize { get; init; }
    public required string MediaType { get; init; }
}

public sealed class ReportCandidate
{
    public required string Id { get; init; }
    public required ReportCandidateKind Kind { get; init; }
    public required DateOnly OccurredLocalDate { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
    public required long Seq { get; init; }
    public required string Body { get; init; }
    public string? Title { get; init; }
    public string? WorkItemId { get; init; }
    public string? WorkTitle { get; init; }
    public string? IssueKey { get; init; }
    public WorkItemStatus? WorkStatusAsOf { get; init; }
    public required string BodyRevision { get; init; }
    public bool OutOfRange { get; init; }
    public IReadOnlyList<ReportFileCandidate> Files { get; init; } = [];
}

public sealed class ReportBuildRequest
{
    public required DateOnly ReportDate { get; init; }
    public required string TimeZoneDisplay { get; init; }
    public required IReadOnlyList<string> SelectedIds { get; init; }
    public required IReadOnlyList<string> SelectedAttachmentIds { get; init; }
    public required IReadOnlyList<ReportCandidate> Catalog { get; init; }
}

public sealed class ReportSnapshotEntry
{
    public required string Id { get; init; }
    public required ReportCandidateKind Kind { get; init; }
    public required DateOnly OccurredLocalDate { get; init; }
    public required string Body { get; init; }
    public required string BodyRevision { get; init; }
    public string? Title { get; init; }
    public string? WorkItemId { get; init; }
    public string? WorkTitle { get; init; }
    public bool OutOfRange { get; init; }
    public string Section { get; init; } = "";
}

public sealed class ReportSnapshotFile
{
    public required string AttachmentId { get; init; }
    public required string EntryId { get; init; }
    public required string OriginalName { get; init; }
    public required string SafeName { get; init; }
    public required string RelativePath { get; init; }
    public required string Sha256 { get; init; }
    public required long ByteSize { get; init; }
}

public sealed class ReportSnapshot
{
    public required string Id { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateOnly ReportDate { get; init; }
    public required string TimeZoneDisplay { get; init; }
    public required string Markdown { get; init; }
    public required string Fingerprint { get; init; }
    public required IReadOnlyList<ReportSnapshotEntry> Entries { get; init; }
    public required IReadOnlyList<ReportSnapshotFile> Files { get; init; }
}
