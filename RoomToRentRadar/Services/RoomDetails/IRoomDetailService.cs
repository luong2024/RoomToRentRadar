using RoomToRentRadar.Models.RoomDetails;

namespace RoomToRentRadar.Services.RoomDetails;

public interface IRoomDetailService
{
    Task<RoomDetailModel?> GetRoomDetailAsync(string roomId, CancellationToken cancellationToken = default);
    Task<List<SimilarRoomModel>> GetSimilarRoomsAsync(string roomId, CancellationToken cancellationToken = default);
}