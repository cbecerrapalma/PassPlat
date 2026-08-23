using System.Data;
using CBP.Results;
using Microsoft.Data.SqlClient;

namespace PassPlat.Aplicacion.Services;

public sealed class SqlDistributedLockService : IDistributedLockService
{
    private readonly string _connectionString;

    public SqlDistributedLockService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Result<IAsyncDisposable>> AcquireLockAsync(string lockName, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        SqlConnection? connection = null;
        SqlTransaction? transaction = null;

        try
        {
            connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            transaction = connection.BeginTransaction();

            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "sp_getapplock";
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(new SqlParameter("@Resource", $"PassPlat:{lockName}"));
            cmd.Parameters.Add(new SqlParameter("@LockMode", "Exclusive"));
            cmd.Parameters.Add(new SqlParameter("@LockOwner", "Transaction"));
            cmd.Parameters.Add(new SqlParameter("@LockTimeout", timeout.HasValue ? (int)timeout.Value.TotalMilliseconds : -1));

            var returnParam = new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.ReturnValue };
            cmd.Parameters.Add(returnParam);

            await cmd.ExecuteNonQueryAsync(ct);

            var result = (int)returnParam.Value;

            if (result >= 0)
                return Result<IAsyncDisposable>.Success(new AppLockHandle(connection, transaction));

            var errorMsg = result switch
            {
                -1 => $"Timeout al adquirir lock '{lockName}'",
                -2 => $"Lock '{lockName}' cancelado",
                -3 => $"Deadlock victim en lock '{lockName}'",
                _ => $"Error al adquirir lock '{lockName}' (código {result})"
            };

            await CleanupAsync(connection, transaction);
            return Result<IAsyncDisposable>.Failure("LOCK_FAILED", errorMsg);
        }
        catch (Exception ex)
        {
            await CleanupAsync(connection, transaction);
            return Result<IAsyncDisposable>.Failure("LOCK_ERROR", $"Error al adquirir lock '{lockName}': {ex.Message}");
        }
    }

    private static async Task CleanupAsync(SqlConnection? connection, SqlTransaction? transaction)
    {
        try { transaction?.Rollback(); transaction?.Dispose(); } catch { }
        try { if (connection != null) await connection.DisposeAsync(); } catch { }
    }

    private sealed class AppLockHandle : IAsyncDisposable
    {
        private readonly SqlConnection _connection;
        private readonly SqlTransaction _transaction;
        private bool _disposed;

        public AppLockHandle(SqlConnection connection, SqlTransaction transaction)
        {
            _connection = connection;
            _transaction = transaction;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            await CleanupAsync(_connection, _transaction);
        }
    }
}
