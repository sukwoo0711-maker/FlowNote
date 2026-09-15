namespace FlowNote.Desktop.ViewModels;

public sealed class AttachmentPreview
{
    public required string Name { get; init; }

    public string? Path { get; init; }

    public required bool IsImage { get; init; }

    public long ByteSize { get; init; }

    public bool Exists { get; init; } = true;

    public bool IsHero { get; init; }
}
