using FlowNote.Core.Errors;
using Microsoft.Data.Sqlite;

namespace FlowNote.Infrastructure.Persistence;

public sealed class SqliteDatabaseExecutor : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public SqliteDatabaseExecutor(string databasePath)
    {
        DatabasePath = databasePath;
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                throw new DatabaseUnavailableException("저장 폴더에 쓸 수 없습니다.", ex);
            }
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        };
        ConnectionString = builder.ToString();
    }

    public string DatabasePath { get; }

    public string ConnectionString { get; }

    public T Write<T>(Func<SqliteConnection, SqliteTransaction, T> work)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _gate.Wait();
        try
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();
            try
            {
                var result = work(connection, transaction);
                transaction.Commit();
                return result;
            }
            catch
            {
                try
                {
                    transaction.Rollback();
                }
                catch (Exception)
                {
                    // Preserve the original failure.
                }

                throw;
            }
        }
        catch (SqliteException ex)
        {
            throw new DatabaseUnavailableException("데이터베이스에 쓰지 못했습니다.", ex);
        }
        catch (IOException ex)
        {
            throw new DatabaseUnavailableException("데이터베이스에 쓰지 못했습니다.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new DatabaseUnavailableException("데이터베이스에 쓸 권한이 없습니다.", ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    public T Read<T>(Func<SqliteConnection, T> work)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _gate.Wait();
        try
        {
            using var connection = Open();
            return work(connection);
        }
        catch (SqliteException ex)
        {
            throw new DatabaseUnavailableException("데이터베이스를 읽지 못했습니다.", ex);
        }
        catch (IOException ex)
        {
            throw new DatabaseUnavailableException("데이터베이스를 읽지 못했습니다.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new DatabaseUnavailableException("데이터베이스를 읽을 권한이 없습니다.", ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gate.Dispose();
        SqliteConnection.ClearAllPools();
    }

    private SqliteConnection Open()
    {
        try
        {
            var connection = new SqliteConnection(ConnectionString);
            connection.DefaultTimeout = 15;
            connection.Open();
            ApplyPragmas(connection);
            return connection;
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            throw new DatabaseUnavailableException("데이터베이스를 열지 못했습니다. 빈 파일로 바꾸지 않습니다.", ex);
        }
    }

    private static void ApplyPragmas(SqliteConnection connection)
    {
        ExecuteNonQuery(connection, "PRAGMA foreign_keys = ON;");
        using (var journal = connection.CreateCommand())
        {
            journal.CommandText = "PRAGMA journal_mode = WAL;";
            journal.ExecuteScalar();
        }

        ExecuteNonQuery(connection, "PRAGMA busy_timeout = 5000;");
        ExecuteNonQuery(connection, "PRAGMA synchronous = NORMAL;");
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
