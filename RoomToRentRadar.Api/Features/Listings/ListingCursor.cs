using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Listings;

public sealed record ListingCursor(
    ListingSort Sort,
    Guid Id,
    string FilterFingerprint,
    DateTime? CreatedAt,
    long? Price,
    double? AreaM2,
    double? DistanceKm = null);
