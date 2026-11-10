namespace RoomToRentRadar.Shared.Listings;

public sealed record ListingDetailDto(
    Guid Id,
    string Title,
    string Description,
    long Price,
    double AreaM2,
    int Bedrooms,
    string Address,
    double Lat,
    double Lng,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<AmenityCode> Amenities,
    IReadOnlyList<string> Rules);
