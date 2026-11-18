namespace RoomToRentRadar.Features.RoomListings.Models;

/// <summary>
/// Room summary DTO returned by the search endpoint (GET api/rooms).
/// </summary>
public sealed record RoomSummaryDto(
    string Id,
    string Title,
    decimal Price,
    double Area,
    string RoomType,
    bool IsVerified,
    double? DistanceInKm)
{
    /// <summary>
    /// Converts this search summary DTO to a presentation item model.
    /// </summary>
    public RoomListItemModel ToListItemModel(string address = "Thái Nguyên")
    {
        return new RoomListItemModel
        {
            Id = Id,
            Title = Title,
            Price = Price,
            AreaSqm = Area,
            Address = address,
            RoomType = RoomType,
            DistanceToCampusKm = DistanceInKm ?? 0.0,
            IsVerified = IsVerified,
            IsAvailable = true
        };
    }
}
