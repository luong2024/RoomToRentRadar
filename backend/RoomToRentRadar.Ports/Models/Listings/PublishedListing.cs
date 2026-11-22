namespace RoomToRentRadar.Ports.Models.Listings;

public sealed record PublishedListing(
    Guid Id,
    string Title,
    long Price,
    double AreaM2,
    double Lat,
    double Lng,
    DateTime UpdatedAt);

public sealed record ListingLocation(double Lat, double Lng);
