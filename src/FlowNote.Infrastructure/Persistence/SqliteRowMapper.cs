using FlowNote.Core.Models;
using Microsoft.Data.Sqlite;

namespace FlowNote.Infrastructure.Persistence;

internal static class SqliteRowMapper
{
    public static TimelineEntry ReadEntry(SqliteDataReader reader)
    {
        return new TimelineEntry
        {
            Seq = reader.GetInt64(reader.GetOrdinal("seq")),
            Id = reader.GetString(reader.GetOrdinal("id")),
            RequestId = reader.GetString(reader.GetOrdinal("request_id")),
            Kind = EntryKindText.Parse(reader.GetString(reader.GetOrdinal("kind"))),
            WorkItemId = GetNullString(reader, "work_item_id"),
            ProjectId = GetNullString(reader, "project_id"),
            TitleSnapshot = GetNullString(reader, "title_snapshot"),
            Body = reader.GetString(reader.GetOrdinal("body")),
            RecordedAtUtc = FlowNote.Core.Time.UtcInstant.Parse(reader.GetString(reader.GetOrdinal("recorded_at_utc"))),
            OccurredAtUtc = FlowNote.Core.Time.UtcInstant.Parse(reader.GetString(reader.GetOrdinal("occurred_at_utc"))),
            OccurredTimeSource = ParseOccurred(reader.GetString(reader.GetOrdinal("occurred_time_source"))),
            RecordedOffsetMinutes = reader.GetInt32(reader.GetOrdinal("recorded_offset_minutes")),
            IssueKey = GetNullString(reader, "issue_key"),
            IssueUrl = GetNullString(reader, "issue_url"),
            ReversesEntryId = GetNullString(reader, "reverses_entry_id"),
            UpdatedAtUtc = GetNullInstant(reader, "updated_at_utc"),
            DeletedAtUtc = GetNullInstant(reader, "deleted_at_utc")
        };
    }

    public static WorkItem ReadWorkItem(SqliteDataReader reader)
    {
        return new WorkItem
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            Title = reader.GetString(reader.GetOrdinal("title")),
            Description = GetNullString(reader, "description"),
            Status = WorkItemStatusText.Parse(reader.GetString(reader.GetOrdinal("status"))),
            ProjectId = GetNullString(reader, "project_id"),
            IssueKey = GetNullString(reader, "issue_key"),
            IssueUrl = GetNullString(reader, "issue_url"),
            CreatedAtUtc = FlowNote.Core.Time.UtcInstant.Parse(reader.GetString(reader.GetOrdinal("created_at_utc"))),
            UpdatedAtUtc = FlowNote.Core.Time.UtcInstant.Parse(reader.GetString(reader.GetOrdinal("updated_at_utc"))),
            CompletedAtUtc = GetNullInstant(reader, "completed_at_utc"),
            Version = reader.GetInt32(reader.GetOrdinal("version")),
            NextActionText = GetNullString(reader, "next_action_text"),
            NextActionSourceEntryId = GetNullString(reader, "next_action_source_entry_id"),
            NextActionSourceRevision = GetNullString(reader, "next_action_source_revision"),
            NextActionUpdatedAtUtc = GetNullInstant(reader, "next_action_updated_at_utc")
        };
    }

    public static string? GetNullString(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    public static DateTimeOffset? GetNullInstant(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : FlowNote.Core.Time.UtcInstant.Parse(reader.GetString(ordinal));
    }

    private static OccurredTimeSource ParseOccurred(string value)
    {
        return value switch
        {
            "recorded" => OccurredTimeSource.Recorded,
            "user" => OccurredTimeSource.User,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}
