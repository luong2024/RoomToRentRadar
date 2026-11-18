using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Moderation;

/// <summary>
/// Position in the listing queue order: oldest first, then listing id ascending
/// as the unique tie-breaker. The status is part of the position because a
/// cursor is only valid for the filter it was issued for.
/// </summary>
public sealed record ModerationListingQueueCursor(
    ListingStatus Status,
    DateTime CreatedAt,
    Guid ListingId);
