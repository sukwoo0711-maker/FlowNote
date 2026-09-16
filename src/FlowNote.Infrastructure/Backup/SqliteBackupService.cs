using System.Security.Cryptography;
using System.Text.Json;
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
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            }.ToString());
            dest.Open();
            source.BackupDatabase(dest);
            dest.Close();
            SqliteConnection.ClearPool(dest);
            return 0;
        });

        CopyDirectory(_database.Paths.AttachmentsDirectory, Path.Combine(destinationDirectory, "attachments"));
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["flownote.db"] = Sha256File(destDb)
        };
        var attachRoot = Path.Combine(destinationDirectory, "attachments");
        if (Directory.Exists(attachRoot))
        {
            foreach (var file in Directory.GetFiles(attachRoot, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(destinationDirectory, file).Replace('\\', '/');
                files[relative] = Sha256File(file);
            }
        }

        var manifest = new BackupManifest
        {
            CreatedAtUtc = DateTimeOffset.UtcNow.ToString("o"),
            IncludesNextAction = true,
            IncludesAssistDerived = true,
            Files = files
        };
        File.WriteAllText(
            Path.Combine(destinationDirectory, "backup-history.json"),
            JsonSerializer.Serialize(new
            {
                created_at_utc = manifest.CreatedAtUtc,
                includes_next_action = true,
                includes_assist_derived = true
            }));
        File.WriteAllText(
            Path.Combine(destinationDirectory, "backup-manifest.json"),
            JsonSerializer.Serialize(manifest));
    }

    public void RestoreFrom(string sourceDirectory, string destinationRoot)
    {
        var sourceDb = Path.Combine(sourceDirectory, "flownote.db");
        if (!File.Exists(sourceDb))
        {
            throw new FileNotFoundException("백업 데이터베이스가 없습니다.", sourceDb);
        }

        var verifyRoot = Path.Combine(Path.GetTempPath(), "flownote-restore-" + Guid.NewGuid().ToString("N"));
        var asideRoot = Path.Combine(destinationRoot, ".restore-aside-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff"));
        Directory.CreateDirectory(verifyRoot);
        try
        {
            File.Copy(sourceDb, Path.Combine(verifyRoot, "flownote.db"), overwrite: false);
            var sourceAttach = Path.Combine(sourceDirectory, "attachments");
            if (Directory.Exists(sourceAttach))
            {
                CopyDirectory(sourceAttach, Path.Combine(verifyRoot, "attachments"));
            }

            var manifestPath = Path.Combine(sourceDirectory, "backup-manifest.json");
            if (File.Exists(manifestPath))
            {
                File.Copy(manifestPath, Path.Combine(verifyRoot, "backup-manifest.json"), overwrite: false);
                ValidateManifest(verifyRoot);
            }

            Directory.CreateDirectory(destinationRoot);
            var destDb = Path.Combine(destinationRoot, "flownote.db");
            var destAttach = Path.Combine(destinationRoot, "attachments");
            if (File.Exists(destDb) || Directory.Exists(destAttach))
            {
                Directory.CreateDirectory(asideRoot);
                if (File.Exists(destDb))
                {
                    File.Move(destDb, Path.Combine(asideRoot, "flownote.db"));
                }

                if (Directory.Exists(destAttach))
                {
                    Directory.Move(destAttach, Path.Combine(asideRoot, "attachments"));
                }
            }

            try
            {
                File.Copy(Path.Combine(verifyRoot, "flownote.db"), destDb, overwrite: false);
                var verifiedAttach = Path.Combine(verifyRoot, "attachments");
                if (Directory.Exists(verifiedAttach))
                {
                    CopyDirectory(verifiedAttach, destAttach);
                }
            }
            catch
            {
                if (Directory.Exists(asideRoot))
                {
                    if (File.Exists(destDb))
                    {
                        File.Delete(destDb);
                    }

                    if (Directory.Exists(destAttach))
                    {
                        Directory.Delete(destAttach, recursive: true);
                    }

                    var asideDb = Path.Combine(asideRoot, "flownote.db");
                    if (File.Exists(asideDb))
                    {
                        File.Move(asideDb, destDb);
                    }

                    var asideAttach = Path.Combine(asideRoot, "attachments");
                    if (Directory.Exists(asideAttach))
                    {
                        Directory.Move(asideAttach, destAttach);
                    }
                }

                throw;
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(verifyRoot))
                {
                    Directory.Delete(verifyRoot, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    private static void ValidateManifest(string root)
    {
        var json = File.ReadAllText(Path.Combine(root, "backup-manifest.json"));
        var manifest = JsonSerializer.Deserialize<BackupManifest>(json)
            ?? throw new InvalidDataException("백업 목록을 읽지 못했습니다.");
        if (manifest.Files.Count == 0 || !manifest.Files.ContainsKey("flownote.db"))
        {
            throw new InvalidDataException("백업 목록에 원본 참조가 없습니다.");
        }

        foreach (var (relative, expected) in manifest.Files)
        {
            var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("백업 파일이 없습니다.", path);
            }

            if (!string.Equals(Sha256File(path), expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("백업 파일이 바뀌었습니다: " + relative);
            }
        }
    }

    private static string Sha256File(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexString(SHA256.HashData(stream));
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

    private sealed class BackupManifest
    {
        public string CreatedAtUtc { get; set; } = "";

        public bool IncludesNextAction { get; set; }

        public bool IncludesAssistDerived { get; set; }

        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
