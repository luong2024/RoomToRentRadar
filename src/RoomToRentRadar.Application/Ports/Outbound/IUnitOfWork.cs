namespace RoomToRentRadar.Application.Ports.Outbound;

/// <summary>
/// Outbound transaction boundary abstraction for atomic persistence operations.
/// Isolates Application use cases from EF Core DbContext transaction mechanics.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
