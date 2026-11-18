using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Shared.Moderation;

/// <summary>Listing summary a moderator needs to review or approve it.</summary>
public sealed record ModerationListingDto(
    Guid Id,
    string Title,
    Guid? OwnerId,
    ListingStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);
