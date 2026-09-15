namespace FlowNote.Core.Models;

public sealed class StoredAttachment
{
    public required string Id { get; init; }
    public required string OriginalName { get; init; }
    public required string StoredRelativePath { get; init; }
    public required string MediaType { get; init; }
    public required long ByteSize { get; init; }
    public required string Sha256 { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public string? ThumbnailRelativePath { get; init; }
}

public sealed class PendingAttachment
{
    public required string OriginalName { get; init; }
    public required string SourcePath { get; init; }
    public required string MediaType { get; init; }
    public long ByteSize { get; init; }
}
