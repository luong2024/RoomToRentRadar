using RoomToRentRadar.Features.RoomListings.Models;

namespace RoomToRentRadar.Features.RoomListings.Services;

/// <summary>
/// Contract for fetching paginated room listings for the RoomListings feature.
/// </summary>
public interface IRoomListingsService
{
    Task<PagedResponse<RoomListItemModel>> GetRoomListingsAsync(
        int page,
        int pageSize,
        RoomSortOption sortBy = RoomSortOption.DistanceAsc,
        string? search = null,
        RoomFilterCriteria? filter = null,
        CancellationToken cancellationToken = default);

    Task<RoomListItemModel?> GetRoomByIdAsync(
        string roomId,
        CancellationToken cancellationToken = default);
}
