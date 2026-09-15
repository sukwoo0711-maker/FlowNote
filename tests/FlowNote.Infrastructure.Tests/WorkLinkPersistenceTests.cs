using FlowNote.Core.Errors;
using FlowNote.Core.Models;

namespace FlowNote.Infrastructure.Tests;

public sealed class WorkLinkPersistenceTests
{
    [Fact]
    public async Task Note_can_be_saved_without_work_and_relinked()
    {
        using var temp = new TempDatabase();
        var a = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "a", Title = "A" });
        var b = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "b", Title = "B" });
        var note = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "n", Body = "분류 없음" });
        Assert.Null(note.WorkItemId);

        var toA = await temp.Database.Entries.RelinkNoteAsync(new RelinkNoteRequest(note.Id, a.WorkItem.Id));
        Assert.Equal(a.WorkItem.Id, toA.WorkItemId);
        Assert.Equal(note.RecordedAtUtc, toA.RecordedAtUtc);
        Assert.Equal("분류 없음", toA.Body);

        var toB = await temp.Database.Entries.RelinkNoteAsync(new RelinkNoteRequest(note.Id, b.WorkItem.Id));
        Assert.Equal(b.WorkItem.Id, toB.WorkItemId);

        var unlinked = await temp.Database.Entries.RelinkNoteAsync(new RelinkNoteRequest(note.Id, null));
        Assert.Null(unlinked.WorkItemId);
    }

    [Fact]
    public async Task Lifecycle_event_cannot_be_relinked()
    {
        using var temp = new TempDatabase();
        var created = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "c", Title = "할 일" });
        var other = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "o", Title = "다른 일" });
        await Assert.ThrowsAsync<ValidationException>(() =>
            temp.Database.Entries.RelinkNoteAsync(new RelinkNoteRequest(created.Event!.Id, other.WorkItem.Id)));
    }

    [Fact]
    public async Task Soft_delete_hides_note_from_work_list()
    {
        using var temp = new TempDatabase();
        var created = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "c", Title = "할 일" });
        var note = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "n",
            Body = "지울 메모",
            WorkItemId = created.WorkItem.Id
        });
        await temp.Database.Entries.SoftDeleteNoteAsync(note.Id);
        Assert.Empty(temp.Database.Entries.ListForWorkItem(created.WorkItem.Id, notesOnly: true));
        var loaded = await temp.Database.Entries.GetByIdAsync(note.Id);
        Assert.NotNull(loaded!.DeletedAtUtc);
    }

    [Fact]
    public async Task Draft_work_item_survives_reopen()
    {
        using var temp = new TempDatabase();
        var created = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "c", Title = "할 일" });
        temp.Database.Drafts.Save("floating-note", "note", "초안", created.WorkItem.Id, null, null);
        temp.Reopen();
        var draft = temp.Database.Drafts.Load("floating-note");
        Assert.Equal(created.WorkItem.Id, draft?.WorkItemId);
        Assert.Equal("초안", draft?.Body);
    }
}
