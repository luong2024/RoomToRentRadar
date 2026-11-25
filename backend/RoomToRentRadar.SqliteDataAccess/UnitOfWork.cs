using Microsoft.EntityFrameworkCore.Storage;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.SqliteDataAccess;

public sealed class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
        => new UnitOfWorkTransaction(
            await dbContext.Database.BeginTransactionAsync(cancellationToken));

    private sealed class UnitOfWorkTransaction(IDbContextTransaction transaction)
        : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken)
            => transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
