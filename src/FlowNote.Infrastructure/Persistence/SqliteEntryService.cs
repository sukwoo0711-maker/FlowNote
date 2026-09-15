using FlowNote.Core.Abstractions;
using FlowNote.Core.Models;
using FlowNote.Core.Rules;
using FlowNote.Core.Time;
using FlowNote.Infrastructure.Attachments;
using Microsoft.Data.Sqlite;

namespace FlowNote.Infrastructure.Persistence;

public sealed class SqliteEntryService : IEntryService
{
    private const string EntryColumns = """
        seq, id, request_id, kind, work_item_id, project_id, title_snapshot, body,
        recorded_at_utc, occurred_at_utc, occurred_time_source, recorded_offset_minutes,
        issue_key, issue_url, reverses_entry_id, updated_at_utc, deleted_at_utc
        """;

    private readonly SqliteDatabaseExecutor _executor;
    private readonly IClock _clock;
    private readonly DisplayTimeZone _displayTimeZone;
    private readonly AttachmentPipeline _attachments;
    private readonly SqliteAssistStore _assist;

    public SqliteEntryService(
        SqliteDatabaseExecutor executor,
        IClock clock,
        DisplayTimeZone displayTimeZone,
        AttachmentPipeline attachments,
        SqliteAssistStore assist)
    {
        _executor = executor;
        _clock = clock;
        _displayTimeZone = displayTimeZone;
        _attachments = attachments;
        _assist = assist;
    }

    public Task<TimelineEntry> SaveNoteAsync(SaveNoteRequest request, CancellationToken cancellationToken = default)
        => SaveNoteAsync(request, Array.Empty<PendingAttachment>(), cancellationToken);

    public Task<TimelineEntry> SaveNoteAsync(
        SaveNoteRequest request,
        IReadOnlyList<PendingAttachment> files,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hasFiles = files.Count > 0;
        NoteRules.ValidateSave(request.Body, request.HasAttachments || hasFiles);

        var prepared = new List<PreparedAttachment>(files.Count);
        for (var i = 0; i < files.Count; i++)
        {
            prepared.Add(_attachments.Prepare(files[i], i));
        }

        var toSave = request;
        if (hasFiles && !request.HasAttachments)
        {
            toSave = new SaveNoteRequest
            {
                RequestId = request.RequestId,
                Body = request.Body,
                Title = request.Title,
                OccurredAtUtc = request.OccurredAtUtc,
                WorkItemId = request.WorkItemId,
                ProjectId = request.ProjectId,
                IssueKey = request.IssueKey,
                IssueUrl = request.IssueUrl,
                HasAttachments = true
            };
        }

        var saved = _executor.Write((connection, transaction) =>
        {
            var existing = FindByRequestId(connection, transaction, toSave.RequestId);
            if (existing is not null)
            {
                return existing;
            }

            var entry = SaveNote(connection, transaction, toSave);
            if (prepared.Count > 0)
            {
                AttachmentPipeline.Insert(connection, transaction, entry.Id, prepared);
            }

            _assist.OnNoteSaved(connection, transaction, entry);
            return entry;
        });
        return Task.FromResult(saved);
    }

    public IReadOnlyList<StoredAttachment> ListAttachments(string entryId)
    {
        return _executor.Read(connection => _attachments.ListForEntry(connection, entryId));
    }

    public IReadOnlyList<TimelineEntry> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var escaped = query.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        var pattern = "%" + escaped + "%";
        return _executor.Read(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT {EntryColumns}
                FROM entries
                WHERE deleted_at_utc IS NULL
                  AND (body LIKE $q ESCAPE '\' OR IFNULL(title_snapshot, '') LIKE $q ESCAPE '\' OR IFNULL(issue_key, '') LIKE $q ESCAPE '\')
                ORDER BY occurred_at_utc DESC, seq DESC
                LIMIT 100;
                """;
            command.Parameters.AddWithValue("$q", pattern);
            using var reader = command.ExecuteReader();
            var list = new List<TimelineEntry>();
            while (reader.Read())
            {
                list.Add(SqliteRowMapper.ReadEntry(reader));
            }

            return (IReadOnlyList<TimelineEntry>)list;
        });
    }

    public Task<IReadOnlyList<TimelineEntry>> ListForLocalDateAsync(DateOnly localDate, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var range = _displayTimeZone.GetUtcRange(localDate);
        var items = _executor.Read(connection => ListRange(connection, range.InclusiveStartUtc, range.ExclusiveEndUtc));
        return Task.FromResult(items);
    }

    public Task<TimelineEntry?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var item = _executor.Read(connection => FindById(connection, transaction: null, id));
        return Task.FromResult(item);
    }

    public Task<TimelineEntry> RelinkNoteAsync(RelinkNoteRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var saved = _executor.Write((connection, transaction) =>
        {
            var current = FindById(connection, transaction, request.EntryId)
                ?? throw new FlowNote.Core.Errors.ValidationException("기록을 찾을 수 없습니다.");
            if (current.Kind != EntryKind.Note)
            {
                throw new FlowNote.Core.Errors.ValidationException("완료·생성 이벤트의 업무는 바꿀 수 없습니다.");
            }

            if (current.DeletedAtUtc is not null)
            {
                throw new FlowNote.Core.Errors.ValidationException("휴지통에 있는 기록은 연결할 수 없습니다.");
            }

            if (string.Equals(current.WorkItemId, request.WorkItemId, StringComparison.Ordinal))
            {
                return current;
            }

            if (request.WorkItemId is not null)
            {
                EnsureWorkItemExists(connection, transaction, request.WorkItemId);
            }

            InsertEntryRevision(connection, transaction, current);
            var now = UtcInstant.TruncateToMilliseconds(_clock.UtcNow);
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE entries
                SET work_item_id = $work,
                    updated_at_utc = $updated
                WHERE id = $id AND kind = 'note';
                """;
            update.Parameters.AddWithValue("$work", (object?)request.WorkItemId ?? DBNull.Value);
            update.Parameters.AddWithValue("$updated", UtcInstant.ToStorage(now));
            update.Parameters.AddWithValue("$id", current.Id);
            if (update.ExecuteNonQuery() != 1)
            {
                throw new FlowNote.Core.Errors.ValidationException("기록 연결을 바꾸지 못했습니다.");
            }

            var relinked = FindById(connection, transaction, current.Id)
                ?? throw new InvalidOperationException("연결을 바꾼 기록을 다시 읽지 못했습니다.");
            _assist.OnNoteRelinked(connection, transaction, relinked);
            return relinked;
        });
        return Task.FromResult(saved);
    }

    public Task<TimelineEntry> UpdateNoteAsync(UpdateNoteRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        NoteRules.ValidateSave(request.Body, hasAttachments: true);
        var saved = _executor.Write((connection, transaction) =>
        {
            var current = FindById(connection, transaction, request.EntryId)
                ?? throw new FlowNote.Core.Errors.ValidationException("기록을 찾을 수 없습니다.");
            if (current.Kind != EntryKind.Note)
            {
                throw new FlowNote.Core.Errors.ValidationException("이벤트 본문은 수정하지 않습니다.");
            }

            InsertEntryRevision(connection, transaction, current);
            var now = UtcInstant.TruncateToMilliseconds(_clock.UtcNow);
            var title = NoteRules.TitleSnapshot(request.Title, request.Body);
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE entries
                SET body = $body,
                    title_snapshot = $title,
                    updated_at_utc = $updated
                WHERE id = $id AND kind = 'note';
                """;
            update.Parameters.AddWithValue("$body", request.Body);
            update.Parameters.AddWithValue("$title", (object?)title ?? DBNull.Value);
            update.Parameters.AddWithValue("$updated", UtcInstant.ToStorage(now));
            update.Parameters.AddWithValue("$id", current.Id);
            update.ExecuteNonQuery();
            var updated = FindById(connection, transaction, current.Id)
                ?? throw new InvalidOperationException("수정한 기록을 다시 읽지 못했습니다.");
            _assist.OnNoteUpdated(connection, transaction, updated);
            return updated;
        });
        return Task.FromResult(saved);
    }

    public Task<TimelineEntry> SoftDeleteNoteAsync(string entryId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var saved = _executor.Write((connection, transaction) =>
        {
            var current = FindById(connection, transaction, entryId)
                ?? throw new FlowNote.Core.Errors.ValidationException("기록을 찾을 수 없습니다.");
            if (current.Kind != EntryKind.Note)
            {
                throw new FlowNote.Core.Errors.ValidationException("이벤트는 휴지통으로 옮기지 않습니다.");
            }

            if (current.DeletedAtUtc is not null)
            {
                return current;
            }

            var now = UtcInstant.TruncateToMilliseconds(_clock.UtcNow);
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE entries SET deleted_at_utc = $deleted WHERE id = $id;";
            update.Parameters.AddWithValue("$deleted", UtcInstant.ToStorage(now));
            update.Parameters.AddWithValue("$id", current.Id);
            update.ExecuteNonQuery();
            _assist.OnNoteDeleted(connection, transaction, current.Id);
            return FindById(connection, transaction, current.Id)
                ?? throw new InvalidOperationException("삭제한 기록을 다시 읽지 못했습니다.");
        });
        return Task.FromResult(saved);
    }

    public IReadOnlyList<TimelineEntry> ListForWorkItem(string workItemId, bool notesOnly)
    {
        return _executor.Read(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT {EntryColumns}
                FROM entries
                WHERE deleted_at_utc IS NULL
                  AND work_item_id = $id
                  AND ($notes_only = 0 OR kind = 'note')
                ORDER BY occurred_at_utc DESC, seq DESC;
                """;
            command.Parameters.AddWithValue("$id", workItemId);
            command.Parameters.AddWithValue("$notes_only", notesOnly ? 1 : 0);
            return ReadAll(command);
        });
    }

    public IReadOnlyList<TimelineEntry> ListNonDeleted()
    {
        return _executor.Read(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT {EntryColumns}
                FROM entries
                WHERE deleted_at_utc IS NULL
                ORDER BY occurred_at_utc ASC, seq ASC;
                """;
            return ReadAll(command);
        });
    }

    public IReadOnlyList<TimelineEntry> ListLifecycleThrough(DateTimeOffset exclusiveEndUtc)
    {
        return _executor.Read(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT {EntryColumns}
                FROM entries
                WHERE deleted_at_utc IS NULL
                  AND occurred_at_utc < $end
                  AND kind IN ('task_created', 'task_completed', 'task_reopened', 'task_cancelled')
                ORDER BY occurred_at_utc ASC, seq ASC;
                """;
            command.Parameters.AddWithValue("$end", UtcInstant.ToStorage(exclusiveEndUtc));
            return ReadAll(command);
        });
    }

    internal TimelineEntry SaveNote(SqliteConnection connection, SqliteTransaction transaction, SaveNoteRequest request)
    {
        var existing = FindByRequestId(connection, transaction, request.RequestId);
        if (existing is not null)
        {
            return existing;
        }

        var recorded = UtcInstant.TruncateToMilliseconds(_clock.UtcNow);
        var occurred = request.OccurredAtUtc is null
            ? recorded
            : UtcInstant.TruncateToMilliseconds(request.OccurredAtUtc.Value);
        var source = request.OccurredAtUtc is null ? OccurredTimeSource.Recorded : OccurredTimeSource.User;
        var id = Guid.NewGuid().ToString("D");

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO entries (
              id, request_id, kind, work_item_id, project_id, title_snapshot, body,
              recorded_at_utc, occurred_at_utc, occurred_time_source, recorded_offset_minutes,
              issue_key, issue_url)
            VALUES (
              $id, $request_id, 'note', $work_item_id, $project_id, $title_snapshot, $body,
              $recorded_at_utc, $occurred_at_utc, $occurred_time_source, $recorded_offset_minutes,
              $issue_key, $issue_url);
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$request_id", request.RequestId);
        command.Parameters.AddWithValue("$work_item_id", (object?)request.WorkItemId ?? DBNull.Value);
        command.Parameters.AddWithValue("$project_id", (object?)request.ProjectId ?? DBNull.Value);
        command.Parameters.AddWithValue("$title_snapshot", (object?)NoteRules.TitleSnapshot(request.Title, request.Body) ?? DBNull.Value);
        command.Parameters.AddWithValue("$body", request.Body);
        command.Parameters.AddWithValue("$recorded_at_utc", UtcInstant.ToStorage(recorded));
        command.Parameters.AddWithValue("$occurred_at_utc", UtcInstant.ToStorage(occurred));
        command.Parameters.AddWithValue("$occurred_time_source", source == OccurredTimeSource.User ? "user" : "recorded");
        command.Parameters.AddWithValue("$recorded_offset_minutes", _displayTimeZone.GetUtcOffsetMinutes(recorded));
        command.Parameters.AddWithValue("$issue_key", (object?)request.IssueKey ?? DBNull.Value);
        command.Parameters.AddWithValue("$issue_url", (object?)request.IssueUrl ?? DBNull.Value);
        command.ExecuteNonQuery();

        return FindById(connection, transaction, id) ?? throw new InvalidOperationException("저장한 기록을 다시 읽지 못했습니다.");
    }

    internal static TimelineEntry? FindByRequestId(SqliteConnection connection, SqliteTransaction? transaction, string requestId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT {EntryColumns} FROM entries WHERE request_id = $request_id LIMIT 1;";
        command.Parameters.AddWithValue("$request_id", requestId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? SqliteRowMapper.ReadEntry(reader) : null;
    }

    internal static TimelineEntry? FindById(SqliteConnection connection, SqliteTransaction? transaction, string id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT {EntryColumns} FROM entries WHERE id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? SqliteRowMapper.ReadEntry(reader) : null;
    }

    private static IReadOnlyList<TimelineEntry> ListRange(SqliteConnection connection, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {EntryColumns}
            FROM entries
            WHERE deleted_at_utc IS NULL
              AND occurred_at_utc >= $start
              AND occurred_at_utc < $end
            ORDER BY occurred_at_utc ASC, seq ASC;
            """;
        command.Parameters.AddWithValue("$start", UtcInstant.ToStorage(startUtc));
        command.Parameters.AddWithValue("$end", UtcInstant.ToStorage(endUtc));
        return ReadAll(command);
    }

    private static IReadOnlyList<TimelineEntry> ReadAll(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        var list = new List<TimelineEntry>();
        while (reader.Read())
        {
            list.Add(SqliteRowMapper.ReadEntry(reader));
        }

        return list;
    }

    private static void EnsureWorkItemExists(SqliteConnection connection, SqliteTransaction transaction, string workItemId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM work_items WHERE id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", workItemId);
        if (command.ExecuteScalar() is null)
        {
            throw new FlowNote.Core.Errors.ValidationException("연결할 할 일을 찾을 수 없습니다.");
        }
    }

    private static void InsertEntryRevision(SqliteConnection connection, SqliteTransaction transaction, TimelineEntry current)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO entry_revisions (
              id, entry_id, changed_at_utc, previous_title, previous_body, previous_occurred_at_utc,
              previous_project_id, previous_work_item_id, previous_issue_key, previous_issue_url)
            VALUES (
              $id, $entry_id, $changed, $title, $body, $occurred,
              $project_id, $work_item_id, $issue_key, $issue_url);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$entry_id", current.Id);
        command.Parameters.AddWithValue("$changed", UtcInstant.ToStorage(DateTimeOffset.UtcNow));
        command.Parameters.AddWithValue("$title", (object?)current.TitleSnapshot ?? DBNull.Value);
        command.Parameters.AddWithValue("$body", current.Body);
        command.Parameters.AddWithValue("$occurred", UtcInstant.ToStorage(current.OccurredAtUtc));
        command.Parameters.AddWithValue("$project_id", (object?)current.ProjectId ?? DBNull.Value);
        command.Parameters.AddWithValue("$work_item_id", (object?)current.WorkItemId ?? DBNull.Value);
        command.Parameters.AddWithValue("$issue_key", (object?)current.IssueKey ?? DBNull.Value);
        command.Parameters.AddWithValue("$issue_url", (object?)current.IssueUrl ?? DBNull.Value);
        command.ExecuteNonQuery();
    }
}
