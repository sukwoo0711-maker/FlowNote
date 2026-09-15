using FlowNote.Core.Abstractions;
using FlowNote.Core.Errors;
using FlowNote.Core.Models;
using FlowNote.Core.Rules;
using FlowNote.Core.Time;
using Microsoft.Data.Sqlite;

namespace FlowNote.Infrastructure.Persistence;

public sealed class SqliteWorkItemService : IWorkItemService
{
    private readonly SqliteDatabaseExecutor _executor;
    private readonly IClock _clock;
    private readonly DisplayTimeZone _displayTimeZone;

    public SqliteWorkItemService(SqliteDatabaseExecutor executor, IClock clock, DisplayTimeZone displayTimeZone)
    {
        _executor = executor;
        _clock = clock;
        _displayTimeZone = displayTimeZone;
    }

    public Task<WorkItemMutationResult> CreateAsync(CreateWorkItemRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var title = WorkItemRules.ValidateTitle(request.Title);
        var result = _executor.Write((connection, transaction) =>
        {
            var existing = SqliteEntryService.FindByRequestId(connection, transaction, request.RequestId);
            if (existing is not null && existing.WorkItemId is not null)
            {
                var item = GetRequired(connection, transaction, existing.WorkItemId);
                return new WorkItemMutationResult(item, existing, AlreadyApplied: true);
            }

            var now = UtcInstant.TruncateToMilliseconds(_clock.UtcNow);
            var id = Guid.NewGuid().ToString("D");
            using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO work_items (
                      id, title, description, status, project_id, issue_key, issue_url,
                      created_at_utc, updated_at_utc, completed_at_utc, version)
                    VALUES (
                      $id, $title, $description, 'open', $project_id, $issue_key, $issue_url,
                      $created, $updated, NULL, 1);
                    """;
                insert.Parameters.AddWithValue("$id", id);
                insert.Parameters.AddWithValue("$title", title);
                insert.Parameters.AddWithValue("$description", (object?)request.Description ?? DBNull.Value);
                insert.Parameters.AddWithValue("$project_id", (object?)request.ProjectId ?? DBNull.Value);
                insert.Parameters.AddWithValue("$issue_key", (object?)request.IssueKey ?? DBNull.Value);
                insert.Parameters.AddWithValue("$issue_url", (object?)request.IssueUrl ?? DBNull.Value);
                insert.Parameters.AddWithValue("$created", UtcInstant.ToStorage(now));
                insert.Parameters.AddWithValue("$updated", UtcInstant.ToStorage(now));
                insert.ExecuteNonQuery();
            }

            var createdEvent = InsertLifecycle(
                connection,
                transaction,
                request.RequestId,
                EntryKind.TaskCreated,
                id,
                request.ProjectId,
                title,
                now,
                reversesEntryId: null);

            return new WorkItemMutationResult(GetRequired(connection, transaction, id), createdEvent, AlreadyApplied: false);
        });

        return Task.FromResult(result);
    }

    public Task<WorkItemMutationResult> CompleteAsync(WorkItemCommandRequest request, CancellationToken cancellationToken = default)
        => MutateAsync(request, WorkItemCommand.Complete, cancellationToken);

    public Task<WorkItemMutationResult> ReopenAsync(WorkItemCommandRequest request, CancellationToken cancellationToken = default)
        => MutateAsync(request, WorkItemCommand.Reopen, cancellationToken);

    public Task<WorkItemMutationResult> CancelAsync(WorkItemCommandRequest request, CancellationToken cancellationToken = default)
        => MutateAsync(request, WorkItemCommand.Cancel, cancellationToken);

    public Task<IReadOnlyList<WorkItem>> ListByStatusAsync(WorkItemStatus status, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var items = _executor.Read(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM work_items WHERE status = $status ORDER BY created_at_utc ASC;";
            command.Parameters.AddWithValue("$status", WorkItemStatusText.ToStorage(status));
            using var reader = command.ExecuteReader();
            var list = new List<WorkItem>();
            while (reader.Read())
            {
                list.Add(SqliteRowMapper.ReadWorkItem(reader));
            }

            return (IReadOnlyList<WorkItem>)list;
        });
        return Task.FromResult(items);
    }

    public Task<WorkItem?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var item = _executor.Read(connection => Find(connection, transaction: null, id));
        return Task.FromResult(item);
    }

    public Task<WorkItemMutationResult> SetNextActionAsync(NextActionRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = _executor.Write((connection, transaction) =>
        {
            var existing = FindRevisionByRequestId(connection, transaction, request.RequestId);
            if (existing is not null)
            {
                return new WorkItemMutationResult(GetRequired(connection, transaction, request.WorkItemId), Event: null, AlreadyApplied: true);
            }

            var current = Find(connection, transaction, request.WorkItemId)
                ?? throw new ValidationException("할 일을 찾을 수 없습니다.");
            if (current.Version != request.ExpectedVersion)
            {
                throw new VersionConflictException("다른 창에서 할 일이 바뀌었습니다. 목록을 다시 불러오세요.");
            }

            NextActionRules.EnsureCanActivate(current.Status, request.Reason);
            var normalized = NextActionRules.ValidateAndNormalize(request.Text, request.Reason);
            var now = UtcInstant.TruncateToMilliseconds(_clock.UtcNow);
            using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE work_items
                    SET next_action_text = $text,
                        next_action_source_entry_id = $source_entry,
                        next_action_source_revision = $source_revision,
                        next_action_updated_at_utc = $changed,
                        updated_at_utc = $updated,
                        version = version + 1
                    WHERE id = $id AND version = $expected;
                    """;
                update.Parameters.AddWithValue("$text", (object?)normalized ?? DBNull.Value);
                update.Parameters.AddWithValue("$source_entry", (object?)request.SourceEntryId ?? DBNull.Value);
                update.Parameters.AddWithValue("$source_revision", (object?)request.SourceRevision ?? DBNull.Value);
                update.Parameters.AddWithValue("$changed", UtcInstant.ToStorage(now));
                update.Parameters.AddWithValue("$updated", UtcInstant.ToStorage(now));
                update.Parameters.AddWithValue("$id", current.Id);
                update.Parameters.AddWithValue("$expected", current.Version);
                if (update.ExecuteNonQuery() != 1)
                {
                    throw new VersionConflictException("다른 창에서 할 일이 바뀌었습니다. 목록을 다시 불러오세요.");
                }
            }

            InsertNextActionRevision(
                connection,
                transaction,
                current,
                request.RequestId,
                request.Reason,
                now,
                current.Version + 1,
                normalized,
                request.SourceEntryId,
                request.SourceRevision);
            return new WorkItemMutationResult(GetRequired(connection, transaction, current.Id), Event: null, AlreadyApplied: false);
        });
        return Task.FromResult(result);
    }

    public IReadOnlyList<NextActionChange> ListNextActionHistory(string workItemId)
    {
        return _executor.Read(connection => ReadHistory(connection, null, workItemId));
    }

    public NextActionAsOf GetNextActionAsOf(string workItemId, DateTimeOffset exclusiveEndUtc)
    {
        var history = ListNextActionHistory(workItemId);
        return NextActionRules.ProjectAsOf(history, exclusiveEndUtc);
    }

    internal static IReadOnlyList<NextActionChange> ReadHistory(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string workItemId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT id, work_item_id, changed_at_utc, request_id, previous_values_json
            FROM work_item_revisions
            WHERE work_item_id = $id
            ORDER BY changed_at_utc ASC, id ASC;
            """;
        command.Parameters.AddWithValue("$id", workItemId);
        using var reader = command.ExecuteReader();
        var list = new List<NextActionChange>();
        while (reader.Read())
        {
            var json = reader.GetString(4);
            if (!json.Contains("\"kind\":\"next_action\"", StringComparison.Ordinal)
                && !json.Contains("\"Kind\":\"next_action\"", StringComparison.Ordinal))
            {
                continue;
            }

            list.Add(NextActionRevisionCodec.ToChange(
                reader.GetString(0),
                reader.GetString(1),
                UtcInstant.Parse(reader.GetString(2)),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                json,
                versionAfter: 0));
        }

        return list;
    }

    private Task<WorkItemMutationResult> MutateAsync(WorkItemCommandRequest request, WorkItemCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = _executor.Write((connection, transaction) =>
        {
            var existingEvent = SqliteEntryService.FindByRequestId(connection, transaction, request.RequestId);
            if (existingEvent is not null && existingEvent.WorkItemId == request.WorkItemId)
            {
                return new WorkItemMutationResult(GetRequired(connection, transaction, request.WorkItemId), existingEvent, AlreadyApplied: true);
            }

            var current = Find(connection, transaction, request.WorkItemId)
                ?? throw new ValidationException("할 일을 찾을 수 없습니다.");
            if (current.Version != request.ExpectedVersion)
            {
                throw new VersionConflictException("다른 창에서 할 일이 바뀌었습니다. 목록을 다시 불러오세요.");
            }

            var decision = WorkItemStateMachine.Decide(current.Status, command);
            if (decision.NoOp)
            {
                return new WorkItemMutationResult(current, Event: null, AlreadyApplied: true);
            }

            var now = UtcInstant.TruncateToMilliseconds(_clock.UtcNow);
            var completedAt = decision.NextStatus == WorkItemStatus.Completed ? now : (DateTimeOffset?)null;
            var clearNext = command is WorkItemCommand.Complete or WorkItemCommand.Cancel;
            using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE work_items
                    SET status = $status,
                        updated_at_utc = $updated,
                        completed_at_utc = $completed,
                        version = version + 1,
                        next_action_text = CASE WHEN $clear_next = 1 THEN NULL ELSE next_action_text END,
                        next_action_source_entry_id = CASE WHEN $clear_next = 1 THEN NULL ELSE next_action_source_entry_id END,
                        next_action_source_revision = CASE WHEN $clear_next = 1 THEN NULL ELSE next_action_source_revision END,
                        next_action_updated_at_utc = CASE WHEN $clear_next = 1 THEN NULL ELSE next_action_updated_at_utc END
                    WHERE id = $id AND version = $expected;
                    """;
                update.Parameters.AddWithValue("$status", WorkItemStatusText.ToStorage(decision.NextStatus));
                update.Parameters.AddWithValue("$updated", UtcInstant.ToStorage(now));
                update.Parameters.AddWithValue("$completed", completedAt is null ? DBNull.Value : UtcInstant.ToStorage(completedAt.Value));
                update.Parameters.AddWithValue("$clear_next", clearNext ? 1 : 0);
                update.Parameters.AddWithValue("$id", current.Id);
                update.Parameters.AddWithValue("$expected", current.Version);
                if (update.ExecuteNonQuery() != 1)
                {
                    throw new VersionConflictException("다른 창에서 할 일이 바뀌었습니다. 목록을 다시 불러오세요.");
                }
            }

            if (clearNext && !string.IsNullOrEmpty(current.NextActionText))
            {
                InsertNextActionRevision(
                    connection,
                    transaction,
                    current,
                    request.RequestId + ":next-action",
                    NextActionChangeReason.Completion,
                    now,
                    current.Version + 1,
                    newText: null,
                    newSourceEntryId: null,
                    newSourceRevision: null);
            }

            string? reverses = null;
            if (command == WorkItemCommand.Reopen)
            {
                reverses = FindLatestCompletedEntryId(connection, transaction, current.Id);
            }

            var timelineEvent = InsertLifecycle(
                connection,
                transaction,
                request.RequestId,
                decision.EventKind ?? throw new InvalidOperationException("상태 변경 이벤트가 없습니다."),
                current.Id,
                current.ProjectId,
                current.Title,
                now,
                reverses);

            return new WorkItemMutationResult(GetRequired(connection, transaction, current.Id), timelineEvent, AlreadyApplied: false);
        });

        return Task.FromResult(result);
    }

    private TimelineEntry InsertLifecycle(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string requestId,
        EntryKind kind,
        string workItemId,
        string? projectId,
        string titleSnapshot,
        DateTimeOffset now,
        string? reversesEntryId)
    {
        var id = Guid.NewGuid().ToString("D");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO entries (
              id, request_id, kind, work_item_id, project_id, title_snapshot, body,
              recorded_at_utc, occurred_at_utc, occurred_time_source, recorded_offset_minutes,
              reverses_entry_id)
            VALUES (
              $id, $request_id, $kind, $work_item_id, $project_id, $title_snapshot, '',
              $recorded_at_utc, $occurred_at_utc, 'recorded', $recorded_offset_minutes,
              $reverses_entry_id);
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$request_id", requestId);
        command.Parameters.AddWithValue("$kind", EntryKindText.ToStorage(kind));
        command.Parameters.AddWithValue("$work_item_id", workItemId);
        command.Parameters.AddWithValue("$project_id", (object?)projectId ?? DBNull.Value);
        command.Parameters.AddWithValue("$title_snapshot", titleSnapshot);
        command.Parameters.AddWithValue("$recorded_at_utc", UtcInstant.ToStorage(now));
        command.Parameters.AddWithValue("$occurred_at_utc", UtcInstant.ToStorage(now));
        command.Parameters.AddWithValue("$recorded_offset_minutes", _displayTimeZone.GetUtcOffsetMinutes(now));
        command.Parameters.AddWithValue("$reverses_entry_id", (object?)reversesEntryId ?? DBNull.Value);
        command.ExecuteNonQuery();
        return SqliteEntryService.FindById(connection, transaction, id)
            ?? throw new InvalidOperationException("업무 이벤트를 다시 읽지 못했습니다.");
    }

    private static string? FindLatestCompletedEntryId(SqliteConnection connection, SqliteTransaction transaction, string workItemId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT id FROM entries
            WHERE work_item_id = $id AND kind = 'task_completed' AND deleted_at_utc IS NULL
            ORDER BY occurred_at_utc DESC, seq DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", workItemId);
        return command.ExecuteScalar() as string;
    }

    private static WorkItem GetRequired(SqliteConnection connection, SqliteTransaction transaction, string id)
    {
        return Find(connection, transaction, id) ?? throw new InvalidOperationException("할 일을 다시 읽지 못했습니다.");
    }

    private static WorkItem? Find(SqliteConnection connection, SqliteTransaction? transaction, string id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT * FROM work_items WHERE id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? SqliteRowMapper.ReadWorkItem(reader) : null;
    }

    private static string? FindRevisionByRequestId(SqliteConnection connection, SqliteTransaction transaction, string requestId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT work_item_id FROM work_item_revisions WHERE request_id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", requestId);
        return command.ExecuteScalar() as string;
    }

    private static void InsertNextActionRevision(
        SqliteConnection connection,
        SqliteTransaction transaction,
        WorkItem current,
        string requestId,
        NextActionChangeReason reason,
        DateTimeOffset now,
        int versionAfter,
        string? newText,
        string? newSourceEntryId,
        string? newSourceRevision)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO work_item_revisions (id, work_item_id, changed_at_utc, previous_values_json, request_id)
            VALUES ($id, $work_item_id, $changed, $json, $request_id);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$work_item_id", current.Id);
        command.Parameters.AddWithValue("$changed", UtcInstant.ToStorage(now));
        command.Parameters.AddWithValue("$json", NextActionRevisionCodec.Serialize(
            requestId,
            reason,
            current.NextActionText,
            current.NextActionSourceEntryId,
            current.NextActionSourceRevision,
            newText,
            newSourceEntryId,
            newSourceRevision));
        command.Parameters.AddWithValue("$request_id", requestId);
        command.ExecuteNonQuery();
        _ = versionAfter;
    }
}
