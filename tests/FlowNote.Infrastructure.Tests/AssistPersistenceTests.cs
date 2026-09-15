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
