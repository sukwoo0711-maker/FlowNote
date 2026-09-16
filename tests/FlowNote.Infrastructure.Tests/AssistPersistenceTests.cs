using FlowNote.Core.Assist;
using FlowNote.Core.Models;
using FlowNote.Infrastructure.Assist;

namespace FlowNote.Infrastructure.Tests;

public sealed class AssistPersistenceTests
{
    [Fact]
    public async Task Assist_migration_preserves_existing_note()
    {
        using var temp = new TempDatabase();
        await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "keep-v3", Body = "기존 원문" });
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(temp.Database.Executor.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT MAX(version) FROM schema_migrations;";
        Assert.Equal(3, Convert.ToInt32(command.ExecuteScalar()));
        var listed = await temp.Reopen().Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14));
        Assert.Equal("기존 원문", Assert.Single(listed).Body);
    }

    [Fact]
    public async Task Save_inserts_outbox_job_in_same_transaction()
    {
        using var temp = new TempDatabase();
        var saved = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "job-1", Body = "분석 대기 메모" });
        var job = Assert.Single(temp.Database.Assist.ListJobs(saved.Id));
        Assert.Equal(AnalysisJobStatus.Pending, job.Status);
        Assert.Equal(saved.Id, job.EntryId);
    }

    [Fact]
    public async Task Duplicate_request_does_not_duplicate_job()
    {
        using var temp = new TempDatabase();
        var first = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "same-job", Body = "한 번" });
        await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "same-job", Body = "두 번" });
        Assert.Single(temp.Database.Assist.ListJobs(first.Id));
    }

    [Fact]
    public async Task Save_does_not_wait_for_slow_model()
    {
        using var temp = new TempDatabase();
        using var worker = new AnalysisWorker(temp.Database, new DelayedFakeInference(TimeSpan.FromSeconds(30)), temp.Clock);
        var started = DateTime.UtcNow;
        var saved = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "slow", Body = "저장은 끝나야 함" });
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(2));
        Assert.Equal("저장은 끝나야 함", saved.Body);
        Assert.Equal(AnalysisJobStatus.Pending, Assert.Single(temp.Database.Assist.ListJobs(saved.Id)).Status);
        _ = worker;
    }

    [Fact]
    public async Task Worker_rules_link_unique_issue_key()
    {
        using var temp = new TempDatabase();
        var work = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest
        {
            RequestId = "w1",
            Title = "기존 업무",
            IssueKey = "DEV-77"
        });
        await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "linked-note",
            Body = "DEV-77 관련 확인",
            WorkItemId = work.WorkItem.Id
        });
        var unlocked = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "follow",
            Body = "DEV-77 후속 로그"
        });
        using var worker = new AnalysisWorker(temp.Database, UnavailableContextInference.Instance, temp.Clock);
        await worker.DrainAsync(8);
        var assignment = temp.Database.Assist.GetAssignment(unlocked.Id);
        Assert.NotNull(assignment);
        Assert.Equal(AssignmentResolution.Assigned, assignment!.Resolution);
        Assert.Equal(ContextRole.Unknown, assignment.Role);
        Assert.Equal(AssignmentOrigin.Rule, assignment.Origin);
        var locked = temp.Database.Assist.GetAssignment(
            (await temp.Database.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14)))
                .First(item => item.RequestId == "linked-note").Id);
        Assert.True(locked?.UserLocked);
    }

    [Fact]
    public async Task Correction_lock_discards_late_result()
    {
        using var temp = new TempDatabase();
        var saved = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "late", Body = "정정 대상" });
        var thread = temp.Database.Assist.CreateThread("사용자 묶음", saved.Id, AssistText.EntryRevision(saved), TitleOrigin.User);
        temp.Database.Assist.CorrectAssignment(saved.Id, "corr-1", thread.Id, createNew: false, newTitle: null);
        var job = Assert.Single(temp.Database.Assist.ListJobs(saved.Id));
        Assert.Equal(AnalysisJobStatus.Stale, job.Status);
        using var worker = new AnalysisWorker(
            temp.Database,
            new DelayedFakeInference(TimeSpan.Zero, _ => new InferenceResult
            {
                EntryId = saved.Id,
                Decision = AssistDecision.New,
                IsFake = true,
                Primary = new InferencePrimary
                {
                    TopicQuote = "정정 대상",
                    Role = ContextRole.Performed,
                    SourceQuote = "정정 대상"
                }
            }),
            temp.Clock);
        await worker.DrainAsync(4);
        var assignment = temp.Database.Assist.GetAssignment(saved.Id);
        Assert.True(assignment?.UserLocked);
        Assert.Equal(thread.Id, assignment?.ThreadId);
    }

    [Fact]
    public async Task Explicit_unlink_does_not_auto_reattach()
    {
        using var temp = new TempDatabase();
        var work = await temp.Database.WorkItems.CreateAsync(new CreateWorkItemRequest { RequestId = "w2", Title = "할 일", IssueKey = "DEV-9" });
        var saved = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "unlink",
            Body = "DEV-9 메모",
            WorkItemId = work.WorkItem.Id
        });
        await temp.Database.Entries.RelinkNoteAsync(new RelinkNoteRequest(saved.Id, null));
        using var worker = new AnalysisWorker(temp.Database, UnavailableContextInference.Instance, temp.Clock);
        await worker.DrainAsync(4);
        var assignment = temp.Database.Assist.GetAssignment(saved.Id);
        Assert.Equal(AssignmentResolution.ManualClear, assignment?.Resolution);
        Assert.True(assignment?.UserLocked);
        Assert.Null(assignment?.ThreadId);
    }

    [Fact]
    public async Task Demo_seed_applies_expected_projection_without_model()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero));
        using var temp = new TempDatabase(clock);
        var fixtures = Path.Combine(FindRepoRoot(), "FlowNote_Local_Assist_V4", "fixtures", "scenarios");
        var map = await FlowNote.Infrastructure.Demo.V4AssistDemoSeeder.SeedDeferredAsync(temp.Database, value => clock.UtcNow = value, fixtures);
        Assert.Equal(5, map.Count);
        var entries = await temp.Database.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 15));
        var assignments = temp.Database.Assist.ListAssignments(entries.Select(static item => item.Id).ToList());
        var threadIds = assignments.Values.Where(static item => item.ThreadId is not null).Select(static item => item.ThreadId!).Distinct();
        var threads = temp.Database.Assist.ListThreadsById(threadIds);
        var projection = DayFlowProjector.Project(entries, assignments, threads);
        Assert.Equal(2, projection.Episodes.Count);
        Assert.Equal(3, projection.Episodes[0].ObservedEntryIds.Count);
        Assert.Single(projection.Episodes[1].ObservedEntryIds);
        Assert.Single(projection.RequestMarkers);
        Assert.Empty(await temp.Database.WorkItems.ListByStatusAsync(WorkItemStatus.Open));
    }

    [Fact]
    public async Task Unlabeled_day_notes_form_ab_return_without_work_labels()
    {
        using var temp = new TempDatabase();
        temp.Database.Assist.SetMode(AssistMode.RulesOnly);
        DateTimeOffset At(int hour, int minute) => new(2026, 9, 14, hour, minute, 0, TimeSpan.FromHours(9));
        await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "prior-board",
            Body = "보드 전원 시퀀스를 확인했다",
            OccurredAtUtc = At(8, 20)
        });
        var script = new (string RequestId, int Hour, int Minute, string Body)[]
        {
            ("ab-1", 9, 0, "인버터 과전류 확인 중"),
            ("ab-2", 9, 20, "코스표 검토 요청 들어옴. 나중에 보기"),
            ("ab-3", 9, 45, "초기화 순서 확인"),
            ("ab-4", 10, 10, "순서 변경 후 재현 안 됨"),
            ("ab-5", 10, 15, "아까 받은 코스표 검토 시작"),
            ("ab-6", 11, 0, "인버터 조건 하나 더 확인"),
            ("ab-x", 11, 30, "확인")
        };
        foreach (var note in script)
        {
            await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
            {
                RequestId = note.RequestId,
                Body = note.Body,
                OccurredAtUtc = At(note.Hour, note.Minute)
            });
        }

        using var worker = new AnalysisWorker(temp.Database, UnavailableContextInference.Instance, temp.Clock);
        await worker.DrainAsync(20);

        var entries = (await temp.Database.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14)))
            .Where(static item => item.Kind == EntryKind.Note)
            .ToDictionary(static item => item.RequestId, StringComparer.Ordinal);
        var assignments = temp.Database.Assist.ListAssignments(entries.Values.Select(static item => item.Id).ToList());
        var threadIds = assignments.Values.Where(static item => item.ThreadId is not null).Select(static item => item.ThreadId!);
        var threads = temp.Database.Assist.ListThreadsById(threadIds);
        var projection = DayFlowProjector.Project(entries.Values.ToList(), assignments, threads);

        Assert.Equal(4, projection.Episodes.Count);
        Assert.Equal(entries["prior-board"].Id, Assert.Single(projection.Episodes[0].ObservedEntryIds));
        Assert.NotEqual(projection.Episodes[0].ThreadId, projection.Episodes[1].ThreadId);
        Assert.Equal(projection.Episodes[1].ThreadId, projection.Episodes[3].ThreadId);
        Assert.NotEqual(projection.Episodes[1].ThreadId, projection.Episodes[2].ThreadId);
        Assert.Equal(
            [entries["ab-1"].Id, entries["ab-3"].Id, entries["ab-4"].Id],
            projection.Episodes[1].ObservedEntryIds);
        Assert.Equal([entries["ab-5"].Id], projection.Episodes[2].ObservedEntryIds);
        Assert.Equal([entries["ab-6"].Id], projection.Episodes[3].ObservedEntryIds);
        Assert.Equal(entries["ab-2"].Id, Assert.Single(projection.RequestMarkers).EntryId);
        Assert.Equal(ContextRole.RequestLater, assignments[entries["ab-2"].Id].Role);
        Assert.Equal(ContextRole.Performed, assignments[entries["ab-4"].Id].Role);
        Assert.Contains(entries["ab-x"].Id, projection.UnclassifiedEntryIds);
        Assert.DoesNotContain(entries.Values, static item => item.WorkItemId is not null);
        Assert.DoesNotContain(
            await temp.Database.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14)),
            static item => item.Kind == EntryKind.TaskCompleted);

        temp.Database.Assist.CorrectAssignment(entries["ab-3"].Id, Guid.NewGuid().ToString("D"), null, false, null);
        await worker.DrainAsync(8);
        var cleared = temp.Database.Assist.GetAssignment(entries["ab-3"].Id);
        Assert.True(cleared is { Resolution: AssignmentResolution.ManualClear, UserLocked: true });
        Assert.Null(cleared.ThreadId);
    }

    [Fact]
    public async Task Relink_keeps_role_and_unlink_stays_unknown()
    {
        using var temp = new TempDatabase();
        temp.Database.Assist.SetMode(AssistMode.RulesOnly);
        var first = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "role-keep",
            Body = "인버터 과전류 확인 중"
        });
        using var worker = new AnalysisWorker(temp.Database, UnavailableContextInference.Instance, temp.Clock);
        await worker.DrainAsync(4);
        var original = temp.Database.Assist.GetAssignment(first.Id);
        Assert.Equal(ContextRole.Performed, original?.Role);
        var other = temp.Database.Assist.CreateThread("다른 묶음", first.Id, AssistText.EntryRevision(first), TitleOrigin.User);
        temp.Database.Assist.CorrectAssignment(first.Id, "relink-1", other.Id, createNew: false, newTitle: null);
        var relinked = temp.Database.Assist.GetAssignment(first.Id);
        Assert.Equal(ContextRole.Performed, relinked?.Role);
        Assert.Equal(other.Id, relinked?.ThreadId);
        temp.Database.Assist.CorrectAssignment(first.Id, "clear-1", null, createNew: false, newTitle: null);
        var cleared = temp.Database.Assist.GetAssignment(first.Id);
        Assert.Equal(AssignmentResolution.ManualClear, cleared?.Resolution);
        Assert.Equal(ContextRole.Unknown, cleared?.Role);
        await worker.DrainAsync(4);
        Assert.Equal(AssignmentResolution.ManualClear, temp.Database.Assist.GetAssignment(first.Id)?.Resolution);
    }

    [Fact]
    public async Task Late_inference_after_edit_does_not_write()
    {
        using var temp = new TempDatabase();
        temp.Database.Assist.SetMode(AssistMode.LocalAssist);
        var saved = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "race-edit",
            Body = "확인"
        });
        var gate = new GateInference();
        using var worker = new AnalysisWorker(temp.Database, gate, temp.Clock);
        using var cts = new CancellationTokenSource();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        worker.Completed += () => finished.TrySetResult();
        var run = worker.RunAsync(cts.Token);
        await gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await temp.Database.Entries.UpdateNoteAsync(new UpdateNoteRequest(saved.Id, "확인 후 수정한 원문"));
        gate.Release.TrySetResult();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();
        try
        {
            await run;
        }
        catch (OperationCanceledException)
        {
        }

        var assignment = temp.Database.Assist.GetAssignment(saved.Id);
        Assert.True(assignment is null || assignment.Origin != AssignmentOrigin.Model);
        Assert.DoesNotContain(
            temp.Database.Assist.ListJobs(saved.Id),
            job => job.Status == AnalysisJobStatus.Succeeded && job.EntryRevision == AssistText.EntryRevision(saved));
    }

    [Fact]
    public async Task Late_inference_after_delete_does_not_revive()
    {
        using var temp = new TempDatabase();
        temp.Database.Assist.SetMode(AssistMode.LocalAssist);
        var saved = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "race-del",
            Body = "확인"
        });
        var gate = new GateInference();
        using var worker = new AnalysisWorker(temp.Database, gate, temp.Clock);
        using var cts = new CancellationTokenSource();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        worker.Completed += () => finished.TrySetResult();
        var run = worker.RunAsync(cts.Token);
        await gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await temp.Database.Entries.SoftDeleteNoteAsync(saved.Id);
        gate.Release.TrySetResult();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();
        try
        {
            await run;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Null(temp.Database.Assist.GetAssignment(saved.Id)?.ThreadId);
        Assert.DoesNotContain(temp.Database.Assist.ListJobs(saved.Id), static job => job.Status == AnalysisJobStatus.Succeeded);
    }

    [Fact]
    public async Task Late_inference_after_correct_does_not_write()
    {
        using var temp = new TempDatabase();
        temp.Database.Assist.SetMode(AssistMode.LocalAssist);
        var saved = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "race-corr",
            Body = "확인"
        });
        var gate = new GateInference();
        using var worker = new AnalysisWorker(temp.Database, gate, temp.Clock);
        using var cts = new CancellationTokenSource();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        worker.Completed += () => finished.TrySetResult();
        var run = worker.RunAsync(cts.Token);
        await gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var thread = temp.Database.Assist.CreateThread("사용자 묶음", saved.Id, AssistText.EntryRevision(saved), TitleOrigin.User);
        temp.Database.Assist.CorrectAssignment(saved.Id, "race-lock", thread.Id, false, null);
        gate.Release.TrySetResult();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();
        try
        {
            await run;
        }
        catch (OperationCanceledException)
        {
        }

        var assignment = temp.Database.Assist.GetAssignment(saved.Id);
        Assert.True(assignment is { UserLocked: true, ThreadId: not null });
        Assert.Equal(thread.Id, assignment!.ThreadId);
    }

    [Fact]
    public async Task Max_attempts_finish_as_failed()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 14, 1, 24, 0, TimeSpan.Zero));
        using var temp = new TempDatabase(clock);
        temp.Database.Assist.SetMode(AssistMode.LocalAssist);
        var saved = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest
        {
            RequestId = "max-try",
            Body = "확인"
        });
        using var worker = new AnalysisWorker(
            temp.Database,
            new DelayedFakeInference(TimeSpan.Zero, request => new InferenceResult
            {
                EntryId = request.EntryId,
                Decision = AssistDecision.Abstain,
                ErrorCode = "model-unavailable-retry",
                IsFake = true
            }),
            clock);
        for (var i = 0; i < 5; i++)
        {
            clock.UtcNow = clock.UtcNow.AddMinutes(1);
            await worker.ProcessOneAsync();
        }

        var job = Assert.Single(temp.Database.Assist.ListJobs(saved.Id));
        Assert.Equal(AnalysisJobStatus.Failed, job.Status);
        Assert.Equal("max-attempts", job.ErrorCode);
    }

    [Fact]
    public async Task Duplicate_request_id_does_not_duplicate_entry()
    {
        using var temp = new TempDatabase();
        var first = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "same-entry", Body = "한 번" });
        var second = await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "same-entry", Body = "두 번" });
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("한 번", second.Body);
        var day = await temp.Database.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14));
        Assert.Equal(1, day.Count(static item => item.RequestId == "same-entry"));
    }

    private static string FindRepoRoot()
    {
        var start = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && start is not null; i++)
        {
            if (File.Exists(Path.Combine(start.FullName, "FlowNote.sln")))
            {
                return start.FullName;
            }

            start = start.Parent;
        }

        throw new DirectoryNotFoundException("repo root");
    }
}
