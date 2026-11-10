namespace RoomToRentRadar.Shared.Listings;

public sealed record ListingSummaryDto(
    Guid Id,
    string Title,
    long Price,
    double AreaM2,
    int Bedrooms,
    string Address,
    double Lat,
    double Lng,
    DateTime CreatedAt,
    string? CoverPhotoUrl);
