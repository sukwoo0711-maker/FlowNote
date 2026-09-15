using FlowNote.Core.Time;
using FlowNote.Infrastructure.Attachments;
using FlowNote.Infrastructure.Backup;
using FlowNote.Infrastructure.Paths;
using FlowNote.Infrastructure.Persistence;
using FlowNote.Infrastructure.Queries;
using FlowNote.Infrastructure.Reports;

namespace FlowNote.Infrastructure;

public sealed class FlowNoteDatabase : IDisposable
{
    public FlowNoteDatabase(AppStoragePaths paths, IClock clock, DisplayTimeZone displayTimeZone)
    {
        Paths = paths;
        DisplayTimeZone = displayTimeZone;
        Executor = new SqliteDatabaseExecutor(paths.DatabasePath);
        new SchemaMigrator(Executor).Apply();
        Attachments = new AttachmentPipeline(paths, clock);
        Assist = new SqliteAssistStore(Executor, clock);
        Entries = new SqliteEntryService(Executor, clock, displayTimeZone, Attachments, Assist);
        WorkItems = new SqliteWorkItemService(Executor, clock, displayTimeZone);
        Drafts = new SqliteDraftStore(Executor, clock);
        Settings = new SqliteSettingsStore(Executor);
        WorkContext = new WorkContextQuery(this);
        Reports = new ReportCatalogBuilder(this);
        ReportZip = new ReportZipWriter();
        Backups = new SqliteBackupService(this);
    }

    public AppStoragePaths Paths { get; }

    public DisplayTimeZone DisplayTimeZone { get; }

    public SqliteDatabaseExecutor Executor { get; }

    public SqliteAssistStore Assist { get; }

    public SqliteEntryService Entries { get; }

    public SqliteWorkItemService WorkItems { get; }

    public SqliteDraftStore Drafts { get; }

    public AttachmentPipeline Attachments { get; }

    public SqliteSettingsStore Settings { get; }

    public WorkContextQuery WorkContext { get; }

    public ReportCatalogBuilder Reports { get; }

    public ReportZipWriter ReportZip { get; }

    public SqliteBackupService Backups { get; }

    public void Dispose()
    {
        Executor.Dispose();
    }
}
