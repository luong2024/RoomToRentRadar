using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Moderation;

public sealed record ModerationListingQueueQuery(ListingStatus Status, int Limit, string? Cursor);
