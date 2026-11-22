using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Ports.Models.Listings;

/// <summary>Inclusive WGS84 bounding box, in degrees.</summary>
public readonly record struct GeoBoundingBox(
    double MinLat,
    double MaxLat,
    double MinLng,
    double MaxLng);

public sealed record ListingDetailSnapshot(ListingStatus Status, ListingDetailDto Detail);

public sealed record ListingContactSnapshot(ListingStatus Status, ListingContactDto Contact);

public sealed record ListingSimilarSubject(Guid Id, int Bedrooms, long Price, double AreaM2);
