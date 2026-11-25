using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Ports.Models.Moderation;

/// <summary>
/// Position in the listing queue order: <c>UpdatedAt DESC, Id DESC</c> for
/// <see cref="ListingStatus.Published"/>, and <c>CreatedAt ASC, Id ASC</c> for
/// <see cref="ListingStatus.PendingReview"/> and other statuses. The status is part
/// of the position because a cursor is only valid for the filter it was issued for.
/// </summary>
public sealed record ModerationListingQueueCursor(
    ListingStatus Status,
    DateTime CreatedAt,
    Guid ListingId);
