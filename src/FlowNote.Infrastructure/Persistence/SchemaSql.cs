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

    public const int AssistVersion = 3;

    public const string Assist = """
        CREATE TABLE context_threads (
          id TEXT PRIMARY KEY,
          title TEXT NOT NULL,
          title_source_entry_id TEXT NULL REFERENCES entries(id),
          title_source_revision TEXT NULL,
          title_origin TEXT NOT NULL CHECK (title_origin IN ('user', 'derived')),
          work_item_id TEXT NULL REFERENCES work_items(id),
          created_at_utc TEXT NOT NULL,
          version INTEGER NOT NULL,
          issue_key TEXT NULL
        );
        CREATE UNIQUE INDEX ux_context_threads_work_item ON context_threads(work_item_id) WHERE work_item_id IS NOT NULL;

        CREATE TABLE context_aliases (
          alias TEXT PRIMARY KEY,
          thread_id TEXT NOT NULL REFERENCES context_threads(id)
        );

        CREATE TABLE entry_context_assignments (
          entry_id TEXT PRIMARY KEY REFERENCES entries(id),
          source_revision TEXT NOT NULL,
          thread_id TEXT NULL REFERENCES context_threads(id),
          origin TEXT NOT NULL CHECK (origin IN ('user', 'rule', 'model')),
          resolution TEXT NOT NULL CHECK (resolution IN ('assigned', 'abstained', 'manual_clear')),
          role TEXT NOT NULL,
          role_origin TEXT NOT NULL CHECK (role_origin IN ('user', 'rule', 'model')),
          source_quote TEXT NULL,
          analysis_run_id TEXT NULL,
          user_locked INTEGER NOT NULL,
          correction_revision INTEGER NOT NULL
        );

        CREATE TABLE context_mentions (
          id TEXT PRIMARY KEY,
          entry_id TEXT NOT NULL REFERENCES entries(id),
          thread_id TEXT NOT NULL REFERENCES context_threads(id),
          role TEXT NOT NULL,
          source_quote TEXT NOT NULL,
          source_revision TEXT NOT NULL,
          origin TEXT NOT NULL,
          next_action_quote TEXT NULL
        );
        CREATE INDEX ix_context_mentions_entry ON context_mentions(entry_id);

        CREATE TABLE action_candidates (
          id TEXT PRIMARY KEY,
          thread_id TEXT NULL REFERENCES context_threads(id),
          entry_id TEXT NOT NULL REFERENCES entries(id),
          source_revision TEXT NOT NULL,
          source_quote TEXT NOT NULL,
          kind TEXT NOT NULL CHECK (kind IN ('request', 'next_action', 'completion_mention')),
          state TEXT NOT NULL CHECK (state IN ('suggested', 'accepted', 'dismissed', 'superseded', 'stale')),
          linked_work_item_id TEXT NULL REFERENCES work_items(id),
          accepted_by_user_at_utc TEXT NULL,
          version INTEGER NOT NULL
        );
        CREATE INDEX ix_action_candidates_entry ON action_candidates(entry_id);

        CREATE TABLE analysis_jobs (
          id TEXT PRIMARY KEY,
          job_key TEXT NOT NULL UNIQUE,
          entry_id TEXT NOT NULL REFERENCES entries(id),
          entry_revision TEXT NOT NULL,
          policy_revision INTEGER NOT NULL,
          rules_version TEXT NOT NULL,
          prompt_version TEXT NOT NULL,
          model_digest TEXT NOT NULL,
          status TEXT NOT NULL,
          attempts INTEGER NOT NULL,
          lease_until_utc TEXT NULL,
          next_attempt_utc TEXT NULL,
          error_code TEXT NULL,
          correction_snapshot INTEGER NOT NULL,
          created_at_utc TEXT NOT NULL,
          updated_at_utc TEXT NOT NULL
        );
        CREATE INDEX ix_analysis_jobs_status ON analysis_jobs(status, next_attempt_utc, created_at_utc);
        CREATE INDEX ix_analysis_jobs_entry ON analysis_jobs(entry_id);

        CREATE TABLE analysis_runs (
          id TEXT PRIMARY KEY,
          job_id TEXT NOT NULL REFERENCES analysis_jobs(id),
          started_at_utc TEXT NOT NULL,
          finished_at_utc TEXT NULL,
          elapsed_ms INTEGER NULL,
          model_tag TEXT NULL,
          model_digest TEXT NULL,
          prompt_version TEXT NULL,
          rules_version TEXT NULL,
          result_json TEXT NULL,
          error_code TEXT NULL
        );

        CREATE TABLE context_corrections (
          id TEXT PRIMARY KEY,
          request_id TEXT NOT NULL UNIQUE,
          entry_id TEXT NOT NULL REFERENCES entries(id),
          previous_json TEXT NOT NULL,
          next_json TEXT NOT NULL,
          changed_at_utc TEXT NOT NULL,
          expected_version INTEGER NOT NULL,
          reason TEXT NOT NULL CHECK (reason IN ('user_changed', 'user_cleared', 'undo'))
        );
        """;
}
