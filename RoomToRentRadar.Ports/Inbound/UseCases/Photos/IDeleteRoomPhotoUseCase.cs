namespace RoomToRentRadar.Ports.Inbound.UseCases.Photos;

// false = không tìm thấy ảnh
public interface IDeleteRoomPhotoUseCase
{
    Task<bool> ExecuteAsync(string roomId, string photoId, CancellationToken ct = default);
}
