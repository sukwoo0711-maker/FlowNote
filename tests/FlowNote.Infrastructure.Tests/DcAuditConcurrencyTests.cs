using FlowNote.Core.Assist;
using FlowNote.Core.Models;
using FlowNote.Infrastructure.Assist;

namespace FlowNote.Infrastructure.Tests;

public sealed class DcAuditConcurrencyTests
{
    [Fact]
    public async Task Late_inference_failure_cannot_revive_an_edited_job()
    {
        using var temp = new TempDatabase();
        temp.Database.Assist.SetMode(AssistMode.LocalAssist);
        var note = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "late-error", Body = "인버터 과전류 확인 중"
        });
        var oldJob = Assert.Single(temp.Database.Assist.ListJobs(note.Id));
        var inference = new FailingGate();
        using var worker = new AnalysisWorker(temp.Database, inference, temp.Clock);
        var running = worker.ProcessOneAsync();
        await inference.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await temp.Database.Entries.UpdateNoteAsync(new UpdateNoteRequest(note.Id, "수정된 인버터 기록", null));
        inference.Release.TrySetResult();
        await running.WaitAsync(TimeSpan.FromSeconds(5));
        var persisted = temp.Database.Assist.ListJobs(note.Id).Single(job => job.Id == oldJob.Id);
        Assert.Equal(AnalysisJobStatus.Stale, persisted.Status);
        Assert.Null(temp.Database.Assist.GetAssignment(note.Id));
        Assert.Equal("수정된 인버터 기록", (await temp.Database.Entries.GetByIdAsync(note.Id))?.Body);
    }

    [Fact]
    public async Task Late_failure_cannot_take_over_a_new_lease()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 16, 0, 0, 0, TimeSpan.Zero));
        using var temp = new TempDatabase(clock);
        var note = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "lease", Body = "인버터 확인 중"
        });
        var first = Assert.IsType<AnalysisJob>(temp.Database.Assist.ClaimNextJob(clock.UtcNow));
        clock.UtcNow = clock.UtcNow.AddSeconds(AssistVersions.LeaseSeconds + 1);
        var second = Assert.IsType<AnalysisJob>(temp.Database.Assist.ClaimNextJob(clock.UtcNow));
        Assert.Equal(first.Id, second.Id);
        temp.Database.Assist.FinishJob(first.Id, AnalysisJobStatus.RetryWait, "late-error", null, TimeSpan.Zero, first.Attempts);
        var live = Assert.Single(temp.Database.Assist.ListJobs(note.Id));
        Assert.Equal(AnalysisJobStatus.Running, live.Status);
        Assert.Equal(second.Attempts, live.Attempts);
    }

    [Fact]
    public async Task Model_fingerprint_change_rejects_in_flight_result()
    {
        using var temp = new TempDatabase();
        temp.Database.Assist.SetMode(AssistMode.LocalAssist);
        temp.Database.Assist.SetLockedDigest("model-old");
        var note = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "fingerprint", Body = "인버터 확인 중"
        });
        var job = Assert.IsType<AnalysisJob>(temp.Database.Assist.ClaimNextJob(temp.Clock.UtcNow));
        temp.Database.Assist.SetLockedDigest("model-new");
        var result = new InferenceResult { EntryId = note.Id, Decision = AssistDecision.Abstain };
        Assert.Equal("stale-analysis-version", temp.Database.Assist.ApplyInference(job, note, result, AssignmentOrigin.Model));
        Assert.Null(temp.Database.Assist.GetAssignment(note.Id));
        Assert.Equal(AnalysisJobStatus.Stale, Assert.Single(temp.Database.Assist.ListJobs(note.Id)).Status);
    }

    private sealed class FailingGate : IContextInference
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsFake => false;
        public bool IsAvailable => true;
        public async Task<InferenceResult> InferAsync(InferenceRequest request, CancellationToken token)
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(token);
            throw new InvalidOperationException("Synthetic inference failure after editing.");
        }
    }
}
