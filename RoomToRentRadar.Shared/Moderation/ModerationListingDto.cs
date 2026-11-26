using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Shared.Moderation;

/// <summary>Listing details a moderator or admin needs to review and approve it.</summary>
public sealed record ModerationListingDto(
    Guid Id,
    string Title,
    Guid? OwnerId,
    ListingStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string Description = "",
    long Price = 0,
    long Deposit = 0,
    double AreaM2 = 0,
    RoomType RoomType = RoomType.SingleRoom,
    int Bedrooms = 0,
    string Address = "",
    double Lat = 0,
    double Lng = 0,
    string LandlordName = "",
    string LandlordPhone = "",
    IReadOnlyList<ListingPhotoDto>? Photos = null);
