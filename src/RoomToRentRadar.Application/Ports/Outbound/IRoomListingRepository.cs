namespace RoomToRentRadar.Application.Ports.Outbound;

using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.ValueObjects;
using RoomToRentRadar.Application.DTOs;

/// <summary>
/// Outbound port for accessing and querying room listings.
/// </summary>
public interface IRoomListingRepository
{
    /// <summary>
    /// Gets one database-paginated page of available and verified room listings for a campus (AC4).
    /// </summary>
    Task<PagedResult<RoomListing>> GetAvailableVerifiedRoomsByCampusAsync(
        string campusId,
        GeoLocation campusLocation,
        int page,
        int pageSize,
        string sortBy,
        bool descending,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        RoomListing roomListing,
        CancellationToken cancellationToken = default);
}
