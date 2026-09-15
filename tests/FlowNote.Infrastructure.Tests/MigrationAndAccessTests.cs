using FlowNote.Core.Errors;
using FlowNote.Core.Models;
using FlowNote.Core.Time;
using FlowNote.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace FlowNote.Infrastructure.Tests;

public sealed class MigrationAndAccessTests
{
    [Fact]
    public async Task Failed_migration_preserves_existing_rows()
    {
        using var temp = new TempDatabase();
        await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "keep", Body = "보존되어야 함" });
        temp.Database.Dispose();

        using var executor = new SqliteDatabaseExecutor(temp.Paths.DatabasePath);
        var failing = new SchemaMigrator(executor,
        [
            new Migration(1, SchemaSql.Initial),
            new Migration(3, "THIS IS NOT VALID SQL ???")
        ]);

        var wrapped = Assert.Throws<DatabaseUnavailableException>(() => failing.Apply());
        Assert.NotNull(wrapped.InnerException);

        using var reopened = new FlowNote.Infrastructure.FlowNoteDatabase(temp.Paths, temp.Clock, temp.TimeZone);
        var listed = await reopened.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14));
        Assert.Equal("보존되어야 함", Assert.Single(listed).Body);
    }

    [Fact]
    public async Task Read_only_database_does_not_replace_file_with_empty_db()
    {
        using var temp = new TempDatabase();
        await temp.Database.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "keep", Body = "원본" });
        temp.Database.Dispose();

        var dbPath = temp.Paths.DatabasePath;
        File.SetAttributes(dbPath, File.GetAttributes(dbPath) | FileAttributes.ReadOnly);
        try
        {
            var thrown = false;
            try
            {
                using var blocked = new FlowNote.Infrastructure.FlowNoteDatabase(temp.Paths, temp.Clock, temp.TimeZone);
                await blocked.Entries.SaveNoteAsync(new SaveNoteRequest { RequestId = "new", Body = "쓰면 안 됨" });
            }
            catch (DatabaseUnavailableException)
            {
                thrown = true;
            }

            Assert.True(thrown);
        }
        finally
        {
            File.SetAttributes(dbPath, File.GetAttributes(dbPath) & ~FileAttributes.ReadOnly);
        }

        using var reopened = new FlowNote.Infrastructure.FlowNoteDatabase(temp.Paths, temp.Clock, temp.TimeZone);
        var listed = await reopened.Entries.ListForLocalDateAsync(new DateOnly(2026, 9, 14));
        Assert.Equal("원본", Assert.Single(listed).Body);
    }

    [Fact]
    public void Journal_mode_is_wal()
    {
        using var temp = new TempDatabase();
        using var connection = new SqliteConnection(temp.Database.Executor.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        Assert.Equal("wal", Convert.ToString(command.ExecuteScalar())!.ToLowerInvariant());
    }
}
