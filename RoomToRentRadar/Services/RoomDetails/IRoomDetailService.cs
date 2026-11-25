using RoomToRentRadar.Models.RoomDetails;

namespace RoomToRentRadar.Services.RoomDetails;

public interface IRoomDetailService
{
    Task<RoomDetailModel?> GetRoomDetailAsync(string roomId, CancellationToken cancellationToken = default);
    Task<List<SimilarRoomModel>> GetSimilarRoomsAsync(string roomId, CancellationToken cancellationToken = default);
    Task<RoomHouseRulesModel?> GetHouseRulesAsync(string roomId, CancellationToken cancellationToken = default);
    Task<List<HouseRulesVersionModel>> GetHouseRulesHistoryAsync(string roomId, CancellationToken cancellationToken = default);
    Task<RoomHouseRulesModel?> UpdateHouseRulesAsync(string roomId, UpdateHouseRulesApiRequest request, CancellationToken cancellationToken = default);
}