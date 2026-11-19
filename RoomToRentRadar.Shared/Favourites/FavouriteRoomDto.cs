using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Shared.Favourites;

public sealed record FavouriteRoomDto(
    Guid ListingId,
    string Title,
    long Price,
    double AreaM2,
    int Bedrooms,
    string Address,
    ListingStatus Status,
    string? CoverPhotoUrl,
    DateTime FavouritedAt);
