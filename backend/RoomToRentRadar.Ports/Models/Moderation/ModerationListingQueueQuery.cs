using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Ports.Models.Moderation;

public sealed record ModerationListingQueueQuery(ListingStatus Status, int Limit, string? Cursor);
