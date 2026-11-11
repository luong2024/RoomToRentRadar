using RoomToRentRadar.Features.RoomListings.Models;

namespace RoomToRentRadar.Features.RoomListings.Services;

/// <summary>
/// Service contract for fetching paginated, sorted, and server-side searched room listings.
/// </summary>
public interface IRoomListingsService
{
    Task<PagedResponse<RoomListItemModel>> GetRoomListingsAsync(
        int page,
        int pageSize,
        RoomSortOption sortBy = RoomSortOption.Newest,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<RoomListItemModel?> GetRoomByIdAsync(
        string roomId,
        CancellationToken cancellationToken = default);
}
