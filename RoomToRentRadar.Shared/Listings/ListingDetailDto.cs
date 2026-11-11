namespace RoomToRentRadar.Shared.Listings;

public sealed record ListingDetailDto(
    Guid Id,
    string Title,
    string Description,
    long Price,
    long Deposit,
    double AreaM2,
    RoomType RoomType,
    int Bedrooms,
    string Address,
    double Lat,
    double Lng,
    ListingStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<AmenityCode> Amenities,
    IReadOnlyList<string> Rules,
    string LandlordName);
