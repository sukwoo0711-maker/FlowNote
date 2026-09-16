using FlowNote.Core.Assist;
using FlowNote.Core.Models;
using FlowNote.Infrastructure.Assist;

namespace FlowNote.Infrastructure.Tests;

public sealed class PostEditConsistencyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Editing_a_note_invalidates_old_semantics_but_keeps_user_link(bool locked)
    {
        using var temp = new TempDatabase();
        var note = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "edit-completion", Body = "센서 검토를 끝냈다"
        });
        using var worker = new AnalysisWorker(temp.Database, UnavailableContextInference.Instance, temp.Clock);
        await worker.DrainAsync(4);
        var before = temp.Database.Assist.GetAssignment(note.Id)!;
        Assert.Equal(ContextRole.CompletionMention, before.Role);
        if (locked)
            temp.Database.Assist.CorrectAssignment(note.Id, "keep-link", before.ThreadId, false, null);
        var edited = await temp.Database.Entries.UpdateNoteAsync(new UpdateNoteRequest(note.Id, "센서 검토는 내일 예정", null));
        var after = temp.Database.Assist.GetAssignment(note.Id)!;
        Assert.Equal(AssistText.EntryRevision(edited), after.SourceRevision);
        Assert.Equal(ContextRole.Unknown, after.Role);
        Assert.Null(after.SourceQuote);
        Assert.Equal(locked, after.UserLocked);
        Assert.Equal(locked ? before.ThreadId : null, after.ThreadId);
        Assert.Equal(locked ? AssignmentResolution.Assigned : AssignmentResolution.Abstained, after.Resolution);
        await worker.DrainAsync(4);
        Assert.Equal(locked ? ContextRole.Unknown : ContextRole.Plan,
            temp.Database.Assist.GetAssignment(note.Id)!.Role);
    }

    [Fact]
    public async Task Editing_a_mixed_note_removes_obsolete_secondary_requests()
    {
        using var temp = new TempDatabase();
        var note = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "mixed-edit", Body = "센서 확인 중. 코스표는 나중에 보기"
        });
        using var worker = new AnalysisWorker(temp.Database, UnavailableContextInference.Instance, temp.Clock);
        await worker.DrainAsync(4);
        Assert.Single(temp.Database.Assist.ListMentions(note.Id));
        await temp.Database.Entries.UpdateNoteAsync(new UpdateNoteRequest(note.Id, "센서 확인 중", null));
        Assert.Empty(temp.Database.Assist.ListMentions(note.Id));
        Assert.DoesNotContain(temp.Database.Assist.ListActionCandidates(note.Id), c => c.State == ActionCandidateState.Suggested);
    }
    [Fact]
    public async Task Editing_a_cleared_note_keeps_the_clear_lock_and_updates_revision()
    {
        using var temp = new TempDatabase();
        var note = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "cleared-edit", Body = "센서 확인 중"
        });
        using var worker = new AnalysisWorker(temp.Database, UnavailableContextInference.Instance, temp.Clock);
        await worker.DrainAsync(4);
        temp.Database.Assist.CorrectAssignment(note.Id, "clear", null, false, null);
        var edited = await temp.Database.Entries.UpdateNoteAsync(new UpdateNoteRequest(note.Id, "코스표 검토 시작", null));
        await worker.DrainAsync(4);
        var assignment = temp.Database.Assist.GetAssignment(note.Id)!;
        Assert.Equal(AssignmentResolution.ManualClear, assignment.Resolution);
        Assert.True(assignment.UserLocked);
        Assert.Null(assignment.ThreadId);
        Assert.Equal(AssistText.EntryRevision(edited), assignment.SourceRevision);
        Assert.Equal("코스표 검토 시작", (await temp.Database.Entries.GetByIdAsync(note.Id))!.Body);
    }
}
