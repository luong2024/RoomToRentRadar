namespace RoomToRentRadar.Ports.Inbound.UseCases.Photos;

/// <summary>
/// Deletes uploads that were never attached to a listing after 24 h (BR-MED-03); returns the number
/// of photos removed. Invoked by the hourly <c>PhotoCleanupService</c>.
/// </summary>
public interface ICleanupExpiredPhotosUseCase
{
    Task<int> ExecuteAsync(CancellationToken ct = default);
}
