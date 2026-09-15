namespace FlowNote.Core.Clipboard;

public sealed class ClipboardImportAttachment
{
    public required string FileName { get; init; }

    public required string MediaType { get; init; }

    public required byte[] Bytes { get; init; }
}

public sealed class ClipboardImportPlan
{
    public IReadOnlyList<string> ExistingFilePaths { get; init; } = [];

    public IReadOnlyList<ClipboardImportAttachment> Attachments { get; init; } = [];

    public string? InsertText { get; init; }

    public bool ConsumesPaste => ExistingFilePaths.Count > 0 || Attachments.Count > 0;
}
