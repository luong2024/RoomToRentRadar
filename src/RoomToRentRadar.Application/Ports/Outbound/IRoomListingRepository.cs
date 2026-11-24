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
    /// Finds a listing projection by its globally unique room ID.
    /// </summary>
    Task<RoomListing?> GetByIdAsync(string roomId, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Removes a listing and its detail projection. Both records must be removed
    /// together so a deleted listing cannot remain available in room details.
    /// </summary>
    Task DeleteAsync(
        string roomId,
        string campusId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for verified and available room listings within a geographic polygon boundary (US05 / T05.2).
    /// </summary>
    Task<PagedResult<RoomListing>> SearchByPolygonAsync(
        GeoPolygon polygon,
        RoomSearchFilter filter,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PagedResult<RoomListing>([], pageIndex, pageSize, 0));
    }
}
