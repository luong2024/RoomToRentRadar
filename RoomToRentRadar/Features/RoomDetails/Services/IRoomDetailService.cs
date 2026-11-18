using RoomToRentRadar.Features.RoomDetails.Models;

namespace RoomToRentRadar.Features.RoomDetails.Services;

public interface IRoomDetailService
{
    Task<RoomDetailModel?> GetRoomDetailAsync(string roomId, CancellationToken cancellationToken = default);
    Task<List<SimilarRoomModel>> GetSimilarRoomsAsync(string roomId, CancellationToken cancellationToken = default);
}