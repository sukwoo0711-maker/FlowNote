using FlowNote.Core.Errors;
using FlowNote.Core.Models;
using FlowNote.Core.Rules;

namespace FlowNote.Infrastructure.Tests;

public sealed class AttachmentPipelineTests
{
    [Fact]
    public async Task Saved_copy_survives_moving_the_original()
    {
        using var temp = new TempDatabase();
        var original = Path.Combine(temp.Root, "sample-uart.log");
        await File.WriteAllTextAsync(original, "uart log");
        var saved = await temp.Database.Entries.SaveNoteAsync(
            new SaveNoteRequest { RequestId = "att-1", Body = "로그 첨부" },
            [new PendingAttachment { OriginalName = "sample-uart.log", SourcePath = original, MediaType = "text/plain" }]);

        File.Move(original, Path.Combine(temp.Root, "moved.log"));
        var attachments = temp.Database.Entries.ListAttachments(saved.Id);
        var copy = Assert.Single(attachments);
        var full = temp.Database.Attachments.ResolveFullPath(copy);
        Assert.True(File.Exists(full));
        Assert.Equal("uart log", await File.ReadAllTextAsync(full));
        Assert.False(string.IsNullOrWhiteSpace(copy.Sha256));
    }

    [Fact]
    public async Task Executable_extension_is_rejected()
    {
        using var temp = new TempDatabase();
        var original = Path.Combine(temp.Root, "tool.exe");
        await File.WriteAllTextAsync(original, "not really an exe");
        await Assert.ThrowsAsync<ValidationException>(() => temp.Database.Entries.SaveNoteAsync(
            new SaveNoteRequest { RequestId = "exe", Body = "실행파일" },
            [new PendingAttachment { OriginalName = "tool.exe", SourcePath = original, MediaType = "application/octet-stream" }]));
        Assert.Empty(await temp.Database.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14)));
    }

    [Fact]
    public async Task Image_only_note_is_saved_without_body_text()
    {
        using var temp = new TempDatabase();
        var original = Path.Combine(temp.Root, "paste.png");
        await File.WriteAllBytesAsync(original, [137, 80, 78, 71, 13, 10, 26, 10]);
        var saved = await temp.Database.Entries.SaveNoteAsync(
            new SaveNoteRequest { RequestId = "img-only", Body = "" },
            [new PendingAttachment { OriginalName = "paste.png", SourcePath = original, MediaType = "image/png" }]);

        Assert.Equal("", saved.Body);
        var copy = Assert.Single(temp.Database.Entries.ListAttachments(saved.Id));
        Assert.Equal("paste.png", copy.OriginalName);
        Assert.True(File.Exists(temp.Database.Attachments.ResolveFullPath(copy)));
    }

    [Fact]
    public void Oversize_is_rejected_without_saving()
    {
        Assert.Throws<ValidationException>(() =>
            AttachmentRules.ValidateFile("huge.png", AppLimits.MaxAttachmentBytes + 1, 0));
    }
}
