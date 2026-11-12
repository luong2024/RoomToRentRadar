namespace RoomToRentRadar.Ports.Outbound.Transactions;

// One active scope per instance; nesting is not supported.
public interface IUnitOfWork
{
    // Commits when work returns, rolls back when work throws.
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default);
}