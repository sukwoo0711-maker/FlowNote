using Microsoft.Data.Sqlite;

namespace FlowNote.Infrastructure.Backup;

public sealed class SqliteBackupService
{
    private readonly FlowNoteDatabase _database;

    public SqliteBackupService(FlowNoteDatabase database)
    {
        _database = database;
    }

    public void BackupTo(string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        var destDb = Path.Combine(destinationDirectory, "flownote.db");
        _database.Executor.Read(source =>
        {
            using var dest = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = destDb,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString());
            dest.Open();
            source.BackupDatabase(dest);
            return 0;
        });

        CopyDirectory(_database.Paths.AttachmentsDirectory, Path.Combine(destinationDirectory, "attachments"));
        File.WriteAllText(
            Path.Combine(destinationDirectory, "backup-history.json"),
            $"{{\"created_at_utc\":\"{DateTimeOffset.UtcNow:o}\",\"includes_next_action\":true,\"includes_assist_derived\":true}}");
    }

    public void RestoreFrom(string sourceDirectory, string destinationRoot)
    {
        var sourceDb = Path.Combine(sourceDirectory, "flownote.db");
        if (!File.Exists(sourceDb))
        {
            throw new FileNotFoundException("백업 데이터베이스가 없습니다.", sourceDb);
        }

        Directory.CreateDirectory(destinationRoot);
        File.Copy(sourceDb, Path.Combine(destinationRoot, "flownote.db"), overwrite: true);
        var attach = Path.Combine(sourceDirectory, "attachments");
        if (Directory.Exists(attach))
        {
            CopyDirectory(attach, Path.Combine(destinationRoot, "attachments"));
        }
    }

    private static void CopyDirectory(string source, string dest)
    {
        if (!Directory.Exists(source))
        {
            return;
        }

        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            if (relative.Contains("..", StringComparison.Ordinal))
            {
                continue;
            }

            var target = Path.Combine(dest, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
