namespace FlowNote.Infrastructure.Persistence;

public sealed class SqliteSettingsStore
{
    private readonly SqliteDatabaseExecutor _executor;

    public SqliteSettingsStore(SqliteDatabaseExecutor executor)
    {
        _executor = executor;
    }

    public string? Get(string key)
    {
        return _executor.Read(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value_json FROM settings WHERE key = $key LIMIT 1;";
            command.Parameters.AddWithValue("$key", key);
            return command.ExecuteScalar() as string;
        });
    }

    public void Set(string key, string valueJson)
    {
        _executor.Write((connection, transaction) =>
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO settings(key, value_json) VALUES ($key, $value)
                ON CONFLICT(key) DO UPDATE SET value_json = excluded.value_json;
                """;
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", valueJson);
            command.ExecuteNonQuery();
            return 0;
        });
    }
}
