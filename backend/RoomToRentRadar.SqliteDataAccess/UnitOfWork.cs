using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.SqliteDataAccess;

public sealed class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateUserEmail(exception))
        {
            throw new DuplicateEmailException(exception);
        }
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
        => new UnitOfWorkTransaction(
            await dbContext.Database.BeginTransactionAsync(cancellationToken));

    private static bool IsDuplicateUserEmail(DbUpdateException exception)
        => exception.InnerException is SqliteException { SqliteErrorCode: 19 } sqliteException
            && sqliteException.Message.Contains("UNIQUE constraint failed: users.email", StringComparison.Ordinal);

    private sealed class UnitOfWorkTransaction(IDbContextTransaction transaction)
        : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken)
            => transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
