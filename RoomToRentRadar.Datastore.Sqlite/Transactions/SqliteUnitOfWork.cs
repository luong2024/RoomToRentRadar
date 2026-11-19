using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Ports.Outbound.Transactions;

namespace RoomToRentRadar.Datastore.Sqlite.Transactions;

/// <summary>
/// SQLite <see cref="IUnitOfWork"/> (US08 create/update listing): opens one connection and one
/// transaction, publishes them through <see cref="SqliteAmbientTransaction"/> so the repositories in
/// <paramref name="action"/> join the same transaction, then commits — or rolls back when the action
/// throws. A nested call joins the outer unit of work instead of starting a second transaction
/// (SQLite has no nested transactions).
/// </summary>
public sealed class SqliteUnitOfWork : IUnitOfWork
{
    private readonly SqliteDbContext _dbContext;

    public SqliteUnitOfWork(SqliteDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (SqliteAmbientTransaction.Current is not null)
        {
            await action(ct);
            return;
        }

        var connection = _dbContext.CreateOpenConnection();
        try
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                using (SqliteAmbientTransaction.Push(connection, transaction))
                {
                    await action(ct);
                }

                transaction.Commit();
            }
            catch
            {
                TryRollback(transaction);
                throw;
            }
        }
        finally
        {
            if (!_dbContext.OwnsSharedConnection)
            {
                connection.Dispose();
            }
        }
    }

    private static void TryRollback(SqliteTransaction transaction)
    {
        try
        {
            transaction.Rollback();
        }
        catch (InvalidOperationException)
        {
            // A failed statement can already have aborted the transaction; disposing still releases it.
        }
        catch (SqliteException)
        {
            // Same as above: the rollback is best-effort, the original exception must surface unchanged.
        }
    }
}
