using FlowNote.Core.Models;
using FlowNote.Infrastructure.Drafts;

namespace FlowNote.Infrastructure.Tests;

public sealed class DraftPersistenceTests
{
    [Fact]
    public void Draft_json_roundtrip_keeps_pending_metadata()
    {
        var pending = new PendingAttachment
        {
            OriginalName = "보드사진.png",
            SourcePath = @"C:\temp\board.png",
            MediaType = "image/png"
        };

        var json = DraftAttachmentCodec.Serialize([pending]);
        var restored = Assert.Single(DraftAttachmentCodec.Deserialize(json));
        Assert.Equal(pending.OriginalName, restored.OriginalName);
        Assert.Equal(pending.SourcePath, restored.SourcePath);
        Assert.Equal(pending.MediaType, restored.MediaType);
    }

    [Fact]
    public async Task Staged_draft_attachment_survives_reopen_then_saves_as_copy()
    {
        using var temp = new TempDatabase();
        var original = Path.Combine(temp.Root, "uart.log");
        await File.WriteAllTextAsync(original, "uart 로그");
        var staged = temp.Database.Attachments.StageIncoming(
            new PendingAttachment
            {
                OriginalName = "uart.log",
                SourcePath = original,
                MediaType = "text/plain"
            },
            currentCount: 0);
        temp.Database.Drafts.Save(
            "floating-note",
            "note",
            "초안 본문",
            null,
            null,
            DraftAttachmentCodec.Serialize([staged]));
        File.Delete(original);

        var reopened = temp.Reopen();
        var draft = reopened.Drafts.Load("floating-note");
        Assert.NotNull(draft);
        Assert.Equal("초안 본문", draft.Body);
        var pending = Assert.Single(DraftAttachmentCodec.Deserialize(draft.StagingAttachmentsJson));
        Assert.True(File.Exists(pending.SourcePath));

        var saved = await reopened.Entries.SaveNoteAsync(
            new SaveNoteRequest { RequestId = "from-draft", Body = draft.Body },
            [pending]);
        reopened.Drafts.Clear("floating-note");

        var attachments = reopened.Entries.ListAttachments(saved.Id);
        var copy = Assert.Single(attachments);
        Assert.Equal("uart 로그", await File.ReadAllTextAsync(reopened.Attachments.ResolveFullPath(copy)));
        Assert.Null(reopened.Drafts.Load("floating-note"));
    }
}
