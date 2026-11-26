using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services.Mapping;

/// <summary>Map tin đăng → pin bản đồ (dùng chung T05.2 spatial search và T07.1 saved search).</summary>
public static class ListingPinMapper
{
    public static SpatialSearchPinDto ToPin(ListingDocument listing) => new()
    {
        Id = listing.Id,
        CampusId = listing.CampusId,
        Title = listing.Title,
        Rent = listing.Rent,
        Area = listing.Area,
        DistanceMeters = listing.DistanceMeters,
        RoomType = listing.RoomType,
        Address = listing.Address,
        ThumbnailUrl = listing.CoverThumbnailUrl(),
        IsVerified = listing.IsVerified,
        Longitude = listing.Location.Coordinates.ElementAtOrDefault(0),
        Latitude = listing.Location.Coordinates.ElementAtOrDefault(1)
    };
}
