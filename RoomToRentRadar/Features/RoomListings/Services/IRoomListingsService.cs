using RoomToRentRadar.Features.RoomListings.Models;

namespace RoomToRentRadar.Features.RoomListings.Services;

/// <summary>
/// Service contract for fetching paginated and sorted room listings.
/// </summary>
public interface IRoomListingsService
{
    Task<PagedResponse<RoomListItemModel>> GetRoomListingsAsync(
        int page,
        int pageSize,
        RoomSortOption sortBy = RoomSortOption.Newest,
        CancellationToken cancellationToken = default);
}
