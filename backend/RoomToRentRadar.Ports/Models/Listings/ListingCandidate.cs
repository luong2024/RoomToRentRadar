namespace RoomToRentRadar.Ports.Models.Listings;

/// <summary>
/// A published listing that already passed the location membership test (radius or
/// polygon). Members are materialized before paging, so every column needed to
/// build the page travels with them.
/// </summary>
public sealed record ListingCandidate(
    Guid Id,
    string Title,
    long Price,
    double AreaM2,
    int Bedrooms,
    string Address,
    double Lat,
    double Lng,
    DateTime CreatedAt,
    double? DistanceKm = null);
