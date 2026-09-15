using FlowNote.Core.Time;

namespace FlowNote.Infrastructure.Persistence;

public sealed class SqliteDraftStore
{
    private readonly SqliteDatabaseExecutor _executor;
    private readonly IClock _clock;

    public SqliteDraftStore(SqliteDatabaseExecutor executor, IClock clock)
    {
        _executor = executor;
        _clock = clock;
    }

    public DraftState? Load(string id)
    {
        return _executor.Read(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT id, mode, body, work_item_id, project_id, staging_attachments_json, updated_at_utc FROM drafts WHERE id = $id LIMIT 1;";
            command.Parameters.AddWithValue("$id", id);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            return new DraftState(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                UtcInstant.Parse(reader.GetString(6)));
        });
    }

    public DraftState Save(string id, string mode, string body, string? workItemId, string? projectId, string? stagingAttachmentsJson)
    {
        var updated = UtcInstant.TruncateToMilliseconds(_clock.UtcNow);
        return _executor.Write((connection, transaction) =>
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO drafts (id, mode, body, work_item_id, project_id, staging_attachments_json, updated_at_utc)
                VALUES ($id, $mode, $body, $work_item_id, $project_id, $staging, $updated)
                ON CONFLICT(id) DO UPDATE SET
                  mode = excluded.mode,
                  body = excluded.body,
                  work_item_id = excluded.work_item_id,
                  project_id = excluded.project_id,
                  staging_attachments_json = excluded.staging_attachments_json,
                  updated_at_utc = excluded.updated_at_utc;
                """;
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$mode", mode);
            command.Parameters.AddWithValue("$body", body);
            command.Parameters.AddWithValue("$work_item_id", (object?)workItemId ?? DBNull.Value);
            command.Parameters.AddWithValue("$project_id", (object?)projectId ?? DBNull.Value);
            command.Parameters.AddWithValue("$staging", (object?)stagingAttachmentsJson ?? DBNull.Value);
            command.Parameters.AddWithValue("$updated", UtcInstant.ToStorage(updated));
            command.ExecuteNonQuery();
            return new DraftState(id, mode, body, workItemId, projectId, stagingAttachmentsJson, updated);
        });
    }

    public void Clear(string id)
    {
        _executor.Write((connection, transaction) =>
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM drafts WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
            return 0;
        });
    }
}

public sealed record DraftState(
    string Id,
    string Mode,
    string Body,
    string? WorkItemId,
    string? ProjectId,
    string? StagingAttachmentsJson,
    DateTimeOffset UpdatedAtUtc);
