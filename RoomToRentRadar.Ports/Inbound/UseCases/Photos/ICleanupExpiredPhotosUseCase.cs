namespace RoomToRentRadar.Ports.Inbound.UseCases.Photos;

public interface ICleanupExpiredPhotosUseCase
{
    Task<int> ExecuteAsync(CancellationToken ct = default);
}
