namespace RoomToRentRadar.Application.Ports.Outbound;

using RoomToRentRadar.Domain.Entities;

/// <summary>
/// Outbound port for accessing and querying room listings.
/// </summary>
public interface IRoomListingRepository
{
    /// <summary>
    /// Gets all available and verified room listings for a specific campus (AC4).
    /// </summary>
    Task<IReadOnlyList<RoomListing>> GetAvailableVerifiedRoomsByCampusAsync(
        string campusId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        RoomListing roomListing,
        CancellationToken cancellationToken = default);
}
