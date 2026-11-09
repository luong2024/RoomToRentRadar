using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Listings;

public sealed record ListingCursor(
    ListingSort Sort,
    Guid Id,
    DateTime? CreatedAt,
    long? Price,
    double? AreaM2);
