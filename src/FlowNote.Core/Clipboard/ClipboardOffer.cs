namespace FlowNote.Core.Clipboard;

public sealed class ClipboardOffer
{
    public IReadOnlyList<string> FilePaths { get; init; } = [];

    public byte[]? Biff12 { get; init; }

    public byte[]? Biff8 { get; init; }

    public string? XmlSpreadsheet { get; init; }

    public string? Html { get; init; }

    public string? Rtf { get; init; }

    public string? Csv { get; init; }

    public string? Text { get; init; }

    public byte[]? Png { get; init; }
}
