namespace RoomToRentRadar.Ports.Outbound.Transactions;

/// <summary>
/// Runs <paramref name="action"/> inside one atomic transaction so the room write and the photo
/// attach/remove writes commit together (US08 create/update listing).
/// </summary>
public interface IUnitOfWork
{
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}
