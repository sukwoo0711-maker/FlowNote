namespace FlowNote.Infrastructure.Persistence;

public static class SchemaSql
{
    public const int InitialVersion = 1;

    public const string Initial = """
        CREATE TABLE schema_migrations (
          version INTEGER PRIMARY KEY,
          applied_at_utc TEXT NOT NULL
        );

        CREATE TABLE projects (
          id TEXT PRIMARY KEY,
          name TEXT NOT NULL,
          created_at_utc TEXT NOT NULL,
          archived_at_utc TEXT NULL
        );

        CREATE TABLE work_items (
          id TEXT PRIMARY KEY,
          title TEXT NOT NULL,
          description TEXT NULL,
          status TEXT NOT NULL CHECK (status IN ('open', 'completed', 'cancelled')),
          project_id TEXT NULL REFERENCES projects(id),
          issue_key TEXT NULL,
          issue_url TEXT NULL,
          created_at_utc TEXT NOT NULL,
          updated_at_utc TEXT NOT NULL,
          completed_at_utc TEXT NULL,
          version INTEGER NOT NULL
        );

        CREATE TABLE entries (
          seq INTEGER PRIMARY KEY AUTOINCREMENT,
          id TEXT NOT NULL UNIQUE,
          request_id TEXT NOT NULL UNIQUE,
          kind TEXT NOT NULL CHECK (kind IN ('note', 'task_created', 'task_completed', 'task_reopened', 'task_cancelled')),
          work_item_id TEXT NULL REFERENCES work_items(id),
          project_id TEXT NULL REFERENCES projects(id),
          title_snapshot TEXT NULL,
          body TEXT NOT NULL,
          recorded_at_utc TEXT NOT NULL,
          occurred_at_utc TEXT NOT NULL,
          occurred_time_source TEXT NOT NULL CHECK (occurred_time_source IN ('recorded', 'user')),
          recorded_offset_minutes INTEGER NOT NULL,
          issue_key TEXT NULL,
          issue_url TEXT NULL,
          reverses_entry_id TEXT NULL REFERENCES entries(id),
          updated_at_utc TEXT NULL,
          deleted_at_utc TEXT NULL
        );

        CREATE INDEX ix_entries_occurred_seq ON entries (occurred_at_utc, seq);
        CREATE INDEX ix_entries_work_item_occurred ON entries (work_item_id, occurred_at_utc);

        CREATE TABLE entry_revisions (
          id TEXT PRIMARY KEY,
          entry_id TEXT NOT NULL REFERENCES entries(id),
          changed_at_utc TEXT NOT NULL,
          previous_title TEXT NULL,
          previous_body TEXT NULL,
          previous_occurred_at_utc TEXT NULL,
          previous_project_id TEXT NULL,
          previous_work_item_id TEXT NULL,
          previous_issue_key TEXT NULL,
          previous_issue_url TEXT NULL,
          previous_attachment_ids_json TEXT NULL
        );

        CREATE TABLE attachments (
          id TEXT PRIMARY KEY,
          original_name TEXT NOT NULL,
          stored_relative_path TEXT NOT NULL,
          media_type TEXT NOT NULL,
          byte_size INTEGER NOT NULL,
          sha256 TEXT NOT NULL,
          created_at_utc TEXT NOT NULL,
          thumbnail_relative_path TEXT NULL
        );

        CREATE TABLE entry_attachments (
          entry_id TEXT NOT NULL REFERENCES entries(id),
          attachment_id TEXT NOT NULL REFERENCES attachments(id),
          sort_order INTEGER NOT NULL,
          PRIMARY KEY (entry_id, attachment_id)
        );

        CREATE INDEX ix_entry_attachments_attachment ON entry_attachments (attachment_id);

        CREATE TABLE work_item_revisions (
          id TEXT PRIMARY KEY,
          work_item_id TEXT NOT NULL REFERENCES work_items(id),
          changed_at_utc TEXT NOT NULL,
          previous_values_json TEXT NOT NULL
        );

        CREATE TABLE drafts (
          id TEXT PRIMARY KEY,
          mode TEXT NOT NULL CHECK (mode IN ('note', 'task')),
          body TEXT NOT NULL,
          work_item_id TEXT NULL,
          project_id TEXT NULL,
          staging_attachments_json TEXT NULL,
          updated_at_utc TEXT NOT NULL
        );

        CREATE TABLE settings (
          key TEXT PRIMARY KEY,
          value_json TEXT NOT NULL
        );
        """;

    public const int NextActionVersion = 2;

    public const string NextAction = """
        ALTER TABLE work_items ADD COLUMN next_action_text TEXT NULL;
        ALTER TABLE work_items ADD COLUMN next_action_source_entry_id TEXT NULL;
        ALTER TABLE work_items ADD COLUMN next_action_source_revision TEXT NULL;
        ALTER TABLE work_items ADD COLUMN next_action_updated_at_utc TEXT NULL;
        ALTER TABLE work_item_revisions ADD COLUMN request_id TEXT NULL;
        CREATE UNIQUE INDEX ux_work_item_revisions_request ON work_item_revisions(request_id) WHERE request_id IS NOT NULL;
        """;
}
