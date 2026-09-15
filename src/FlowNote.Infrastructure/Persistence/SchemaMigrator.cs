using FlowNote.Core.Time;
using Microsoft.Data.Sqlite;

namespace FlowNote.Infrastructure.Persistence;

public sealed class SchemaMigrator
{
    private readonly SqliteDatabaseExecutor _executor;
    private readonly IReadOnlyList<Migration> _migrations;

    public SchemaMigrator(SqliteDatabaseExecutor executor, IReadOnlyList<Migration>? migrations = null)
    {
        _executor = executor;
        _migrations = migrations ?? DefaultMigrations;
    }

    public static IReadOnlyList<Migration> DefaultMigrations { get; } =
    [
        new Migration(SchemaSql.InitialVersion, SchemaSql.Initial),
        new Migration(SchemaSql.NextActionVersion, SchemaSql.NextAction)
    ];

    public int Apply()
    {
        return _executor.Write((connection, transaction) =>
        {
            var current = ReadCurrentVersion(connection, transaction);
            foreach (var migration in _migrations.OrderBy(static item => item.Version))
            {
                if (migration.Version <= current)
                {
                    continue;
                }

                foreach (var statement in SplitStatements(migration.Sql))
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandTimeout = 15;
                    command.CommandText = statement;
                    command.ExecuteNonQuery();
                }

                using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = "INSERT INTO schema_migrations(version, applied_at_utc) VALUES ($version, $applied);";
                    insert.Parameters.AddWithValue("$version", migration.Version);
                    insert.Parameters.AddWithValue("$applied", UtcInstant.ToStorage(DateTimeOffset.UtcNow));
                    insert.ExecuteNonQuery();
                }

                current = migration.Version;
            }

            return current;
        });
    }

    private static IEnumerable<string> SplitStatements(string sql)
    {
        return sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static statement => statement.Length > 0);
    }

    private static int ReadCurrentVersion(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var exists = connection.CreateCommand();
        exists.Transaction = transaction;
        exists.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'schema_migrations';";
        var tableCount = Convert.ToInt32(exists.ExecuteScalar());
        if (tableCount == 0)
        {
            return 0;
        }

        using var version = connection.CreateCommand();
        version.Transaction = transaction;
        version.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";
        return Convert.ToInt32(version.ExecuteScalar());
    }
}

public sealed record Migration(int Version, string Sql);
