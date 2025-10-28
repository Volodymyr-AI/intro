using System.Data.SQLite;
using Microsoft.Extensions.Logging;

namespace PMSIntegration.Infrastructure.Database;

public class DatabaseContext : IDisposable
{
    private SQLiteConnection? _connection;
    private readonly string _connectionString;
    private readonly object _lockObject = new  object();
    private bool _disposed;

    public DatabaseContext(string databasePath)
    {
        _connectionString = $"Data Source={databasePath}; Version=3;Pooling=true;Max Pool Size=10;Journal Mode=WAL;";
    }

    public SQLiteConnection Connection
    {
        get
        {
            lock (_lockObject)
            {
                if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
                {
                    _connection = new SQLiteConnection(_connectionString);
                    _connection.Open();
                }
                return _connection;
            }
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<SQLiteTransaction, Task<T>> action)
    {
        using var transaction = Connection.BeginTransaction();
        try
        {
            var result = await action(transaction);
            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (_connection?.State == System.Data.ConnectionState.Open)
        {
            _connection.Close();
        }
        
        _connection?.Dispose();
        _disposed = true;
    }
}