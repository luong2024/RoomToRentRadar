using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Ports.Models.Photos;

public sealed record PhotoListingInfo(Guid? OwnerId, ListingStatus Status);

public sealed record PhotoSlot(int SortOrder, bool IsCover);
