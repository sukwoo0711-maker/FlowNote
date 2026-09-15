namespace FlowNote.Core.Models;

public enum NextActionChangeReason
{
    Set,
    Update,
    Clear,
    Completion,
    Restore
}

public sealed class NextActionRequest
{
    public required string WorkItemId { get; init; }
    public required string RequestId { get; init; }
    public required int ExpectedVersion { get; init; }
    public string? Text { get; init; }
    public string? SourceEntryId { get; init; }
    public string? SourceRevision { get; init; }
    public required NextActionChangeReason Reason { get; init; }
}

public sealed record NextActionChange(
    string Id,
    string WorkItemId,
    DateTimeOffset ChangedAtUtc,
    string RequestId,
    NextActionChangeReason Reason,
    string? PreviousText,
    string? PreviousSourceEntryId,
    string? PreviousSourceRevision,
    string? NewText,
    string? NewSourceEntryId,
    string? NewSourceRevision,
    int VersionAfter);

public sealed record NextActionAsOf(
    string? Text,
    bool Recorded,
    string? SourceEntryId,
    string? SourceRevision);

public sealed record RelinkNoteRequest(string EntryId, string? WorkItemId);

public sealed record UpdateNoteRequest(string EntryId, string Body, string? Title = null);

public static class NextActionChangeReasonText
{
    public static string ToStorage(NextActionChangeReason reason) => reason switch
    {
        NextActionChangeReason.Set => "set",
        NextActionChangeReason.Update => "update",
        NextActionChangeReason.Clear => "clear",
        NextActionChangeReason.Completion => "completion",
        NextActionChangeReason.Restore => "restore",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
    };

    public static NextActionChangeReason Parse(string value) => value switch
    {
        "set" => NextActionChangeReason.Set,
        "update" => NextActionChangeReason.Update,
        "clear" => NextActionChangeReason.Clear,
        "completion" => NextActionChangeReason.Completion,
        "restore" => NextActionChangeReason.Restore,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };
}
