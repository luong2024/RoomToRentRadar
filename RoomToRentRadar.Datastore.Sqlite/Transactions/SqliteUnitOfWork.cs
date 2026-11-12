using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Ports.Outbound.Transactions;

namespace RoomToRentRadar.Datastore.Sqlite.Transactions;

public sealed class SqliteUnitOfWork : IUnitOfWork
{
    private readonly SqliteDbContext _dbContext;

    public SqliteUnitOfWork(SqliteDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    internal SqliteConnection? CurrentConnection { get; private set; }

    internal SqliteTransaction? CurrentTransaction { get; private set; }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var transaction = conn.BeginTransaction();
            CurrentConnection = conn;
            CurrentTransaction = transaction;
            try
            {
                var result = await work(ct);
                await transaction.CommitAsync(ct);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
            finally
            {
                CurrentConnection = null;
                CurrentTransaction = null;
            }
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    private void ReleaseConnection(SqliteConnection conn)
    {
        if (!_dbContext.OwnsSharedConnection)
        {
            conn.Dispose();
        }
    }
}