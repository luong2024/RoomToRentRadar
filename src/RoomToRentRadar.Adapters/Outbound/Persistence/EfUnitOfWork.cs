using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;

namespace RoomToRentRadar.Adapters.Outbound.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private const int SqliteConstraintErrorCode = 19;

    private readonly AppDbContext _dbContext;

    public EfUnitOfWork(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateEmailConstraintViolation(ex))
        {
            throw new DuplicateEmailException(ex);
        }
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_dbContext.Database.CurrentTransaction is not null)
        {
            await operation(cancellationToken);
            return;
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_dbContext.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static bool IsDuplicateEmailConstraintViolation(DbUpdateException exception)
    {
        if (exception.InnerException is SqliteException sqliteException &&
            sqliteException.SqliteErrorCode == SqliteConstraintErrorCode)
        {
            return sqliteException.Message.Contains("Users.Email", StringComparison.OrdinalIgnoreCase) ||
                   sqliteException.Message.Contains("IX_Users_Email", StringComparison.OrdinalIgnoreCase);
        }

        return exception.InnerException?.Message.Contains("IX_Users_Email", StringComparison.OrdinalIgnoreCase) == true;
    }
}
