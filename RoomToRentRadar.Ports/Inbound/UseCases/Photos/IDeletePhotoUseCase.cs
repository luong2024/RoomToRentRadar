namespace RoomToRentRadar.Ports.Inbound.UseCases.Photos;

/// <summary>
/// Deletes an unattached photo owned by the caller (<c>operationId: deletePhoto</c>, BR-MED-03).
/// Attached photos are a 409 INVALID_STATE_TRANSITION; other owners' photos are a 404 (anti-IDOR).
/// </summary>
public interface IDeletePhotoUseCase
{
    Task ExecuteAsync(string userId, string photoId, CancellationToken ct = default);
}
