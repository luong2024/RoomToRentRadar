namespace RoomToRentRadar.Ports.Inbound;

public interface ISavedSearchAlertUseCase
{
    Task<int> SweepAsync(DateTime utcNow, CancellationToken cancellationToken);
}
