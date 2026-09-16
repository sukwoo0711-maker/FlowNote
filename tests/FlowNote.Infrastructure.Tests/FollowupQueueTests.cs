using FlowNote.Core.Assist;
using FlowNote.Core.Models;
using FlowNote.Infrastructure.Assist;

namespace FlowNote.Infrastructure.Tests;

public sealed class FollowupQueueTests
{
    [Fact]
    public async Task Final_expired_lease_becomes_terminal()
    {
        var clock = new FakeClock(DateTimeOffset.Parse("2026-09-16T00:00:00Z"));
        using var temp = new TempDatabase(clock);
        var note = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "final-lease", Body = "센서 점검 중" });
        for (var i = 0; i < AssistVersions.MaxAttempts; i++)
        {
            Assert.NotNull(temp.Database.Assist.ClaimNextJob(clock.UtcNow));
            clock.UtcNow = clock.UtcNow.AddSeconds(AssistVersions.LeaseSeconds + 1);
        }
        Assert.Null(temp.Database.Assist.ClaimNextJob(clock.UtcNow));
        var job = Assert.Single(temp.Database.Assist.ListJobs(note.Id));
        Assert.Equal(AnalysisJobStatus.Failed, job.Status);
        Assert.Null(job.LeaseUntilUtc);
        Assert.NotNull(await temp.Database.Entries.GetByIdAsync(note.Id));
    }

    [Fact]
    public async Task Expired_worker_cannot_apply_before_another_claim()
    {
        var clock = new FakeClock(DateTimeOffset.Parse("2026-09-16T00:00:00Z"));
        using var temp = new TempDatabase(clock);
        var note = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "expired-result", Body = "센서 점검 중" });
        var job = temp.Database.Assist.ClaimNextJob(clock.UtcNow)!;
        clock.UtcNow = clock.UtcNow.AddSeconds(AssistVersions.LeaseSeconds + 1);
        var result = new InferenceResult
        {
            EntryId = note.Id, Decision = AssistDecision.New,
            Primary = new InferencePrimary { TopicQuote = "센서", SourceQuote = "센서", Role = ContextRole.Performed }
        };
        Assert.Equal("stale-lease", temp.Database.Assist.ApplyInference(job, note, result, AssignmentOrigin.Rule));
        Assert.Null(temp.Database.Assist.GetAssignment(note.Id));
    }

    [Fact]
    public async Task Inference_timeout_does_not_cancel_the_worker()
    {
        using var temp = new TempDatabase();
        temp.Database.Assist.SetMode(AssistMode.LocalAssist);
        var note = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "timeout", Body = "센서 점검 중" });
        using var worker = new AnalysisWorker(temp.Database, new TimeoutInference(), temp.Clock);
        Assert.True(await worker.ProcessOneAsync());
        var job = Assert.Single(temp.Database.Assist.ListJobs(note.Id));
        Assert.Equal(AnalysisJobStatus.RetryWait, job.Status);
        Assert.Equal("model-timeout", job.ErrorCode);
    }

    [Fact]
    public async Task Caller_cancellation_keeps_a_retryable_job()
    {
        var clock = new FakeClock(DateTimeOffset.Parse("2026-09-16T00:00:00Z"));
        using var temp = new TempDatabase(clock);
        temp.Database.Assist.SetMode(AssistMode.LocalAssist);
        var note = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "cancel", Body = "센서 점검 중" });
        var inference = new GateInference();
        using var worker = new AnalysisWorker(temp.Database, inference, clock);
        using var cts = new CancellationTokenSource();
        var task = worker.ProcessOneAsync(cts.Token);
        await inference.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        var job = Assert.Single(temp.Database.Assist.ListJobs(note.Id));
        Assert.Equal(AnalysisJobStatus.RetryWait, job.Status);
        clock.UtcNow = clock.UtcNow.AddSeconds(6);
        Assert.NotNull(temp.Database.Assist.ClaimNextJob(clock.UtcNow));
        Assert.Equal("센서 점검 중", (await temp.Database.Entries.GetByIdAsync(note.Id))!.Body);
    }

    [Fact]
    public async Task Active_final_lease_is_not_failed_by_a_poll()
    {
        var clock = new FakeClock(DateTimeOffset.Parse("2026-09-16T00:00:00Z"));
        using var temp = new TempDatabase(clock);
        var note = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "active", Body = "센서 점검 중" });
        for (var i = 0; i < AssistVersions.MaxAttempts; i++)
        {
            Assert.NotNull(temp.Database.Assist.ClaimNextJob(clock.UtcNow));
            if (i < AssistVersions.MaxAttempts - 1)
                clock.UtcNow = clock.UtcNow.AddSeconds(AssistVersions.LeaseSeconds + 1);
        }
        Assert.Null(temp.Database.Assist.ClaimNextJob(clock.UtcNow));
        Assert.Equal(AnalysisJobStatus.Running, Assert.Single(temp.Database.Assist.ListJobs(note.Id)).Status);
    }

    [Fact]
    public async Task Already_cancelled_call_does_not_claim_a_note()
    {
        using var temp = new TempDatabase();
        var note = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "not-claimed", Body = "센서 점검 중" });
        using var worker = new AnalysisWorker(temp.Database, UnavailableContextInference.Instance, temp.Clock);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker.ProcessOneAsync(new CancellationToken(true)));
        var job = Assert.Single(temp.Database.Assist.ListJobs(note.Id));
        Assert.Equal(AnalysisJobStatus.Pending, job.Status);
        Assert.Equal(0, job.Attempts);
    }

    [Fact]
    public async Task Repeated_timeouts_obey_the_existing_retry_limit()
    {
        var clock = new FakeClock(DateTimeOffset.Parse("2026-09-16T00:00:00Z"));
        using var temp = new TempDatabase(clock);
        temp.Database.Assist.SetMode(AssistMode.LocalAssist);
        var note = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "timeouts", Body = "센서 점검 중" });
        using var worker = new AnalysisWorker(temp.Database, new TimeoutInference(), clock);
        for (var i = 0; i < AssistVersions.MaxAttempts; i++)
        {
            Assert.True(await worker.ProcessOneAsync());
            clock.UtcNow = clock.UtcNow.AddSeconds(31);
        }
        var job = Assert.Single(temp.Database.Assist.ListJobs(note.Id));
        Assert.Equal(AnalysisJobStatus.Failed, job.Status);
        Assert.Null(job.NextAttemptUtc);
        Assert.False(await worker.ProcessOneAsync());
    }

    private sealed class TimeoutInference : IContextInference
    {
        public bool IsFake => true;
        public bool IsAvailable => true;
        public Task<InferenceResult> InferAsync(InferenceRequest request, CancellationToken cancellationToken)
            => Task.FromException<InferenceResult>(new TaskCanceledException("inference deadline"));
    }
}
