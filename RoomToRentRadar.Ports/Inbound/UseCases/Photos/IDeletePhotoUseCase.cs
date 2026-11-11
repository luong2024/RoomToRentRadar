namespace RoomToRentRadar.Ports.Inbound.UseCases.Photos;

public enum DeletePhotoResult
{
    Deleted,
    NotFound,
    Attached
}

public interface IDeletePhotoUseCase
{
    Task<DeletePhotoResult> ExecuteAsync(string photoId, string userId, CancellationToken ct = default);
}
