namespace RoomToRentRadar.Shared.Favourites;

public sealed record FavouriteListingDto(
    Guid ListingId,
    string Title,
    long Price,
    double AreaM2,
    int Bedrooms,
    string Address,
    double Lat,
    double Lng,
    string? CoverPhotoUrl,
    bool IsAvailable,
    DateTime FavouritedAt);
