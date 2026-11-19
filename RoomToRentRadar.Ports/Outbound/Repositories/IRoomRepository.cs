namespace RoomToRentRadar.Ports.Outbound.Repositories;

using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;

/// <summary>
/// One listing of the landlord's own-listings page. <see cref="CoverThumbPath"/> is the thumb storage
/// key of the photo with the lowest <c>SortOrder</c> (null when the room has no photo yet).
/// </summary>
public sealed record OwnedRoomListing(Room Room, string? CoverThumbPath);

/// <summary>
/// One page of a landlord's listings plus the status counters used by the SCR-LL-01 filter chips:
/// <see cref="CountsByStatus"/> always contains all six <see cref="ListingStatus"/> values (0 when the
/// landlord has none), computed over all of the landlord's rooms and ignoring the page filter.
/// </summary>
public sealed record PagedOwnedRooms(
    IReadOnlyList<OwnedRoomListing> Items,
    int TotalCount,
    IReadOnlyDictionary<ListingStatus, int> CountsByStatus);

/// <summary>Lifecycle storage of the Room aggregate root (write side and by-id reads).</summary>
public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(string id, CancellationToken ct = default);
    Task AddAsync(Room room, CancellationToken ct = default);
    Task UpdateAsync(Room room, CancellationToken ct = default);

    /// <summary>
    /// Ownership-scoped read (anti-IDOR, GUARDRAILS §1.5.6): returns the room with its amenities, safety
    /// features and current house rules, or null when the room does not exist OR belongs to another
    /// landlord — the caller cannot tell the two cases apart.
    /// </summary>
    Task<Room?> GetOwnedByIdAsync(string id, string landlordId, CancellationToken ct = default);

    /// <summary>BR-LST-11: how many listings the landlord owns that are not <c>Unpublished</c> (max 50).</summary>
    Task<int> CountNonArchivedByLandlordAsync(string landlordId, CancellationToken ct = default);

    /// <summary>
    /// Own-listings page (UC-31) for one landlord: optional status filter, ordered by
    /// <c>LastUpdatedAtUtc</c> descending then <c>Id</c> ascending. Items do not carry child collections;
    /// use <see cref="GetOwnedByIdAsync"/> for the editable detail.
    /// </summary>
    Task<PagedOwnedRooms> ListByLandlordAsync(
        string landlordId,
        ListingStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default);
}
