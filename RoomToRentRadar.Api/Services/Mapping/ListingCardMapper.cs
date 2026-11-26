using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services.Mapping;

/// <summary>Map <see cref="ListingDocument"/> sang card danh sách — dùng chung cho chi tiết phòng (T02.1) và tìm kiếm (T04.1).</summary>
public static class ListingCardMapper
{
    public static ListingCardDto ToCard(ListingDocument listing) => new()
    {
        Id = listing.Id,
        CampusId = listing.CampusId,
        Title = listing.Title,
        Rent = listing.Rent,
        Area = listing.Area,
        DistanceMeters = listing.DistanceMeters,
        RoomType = listing.RoomType,
        Address = listing.Address,
        ThumbnailUrl = listing.CoverPhotoUrl(),
        Status = listing.Status,
        IsVerified = listing.IsVerified
    };
}
