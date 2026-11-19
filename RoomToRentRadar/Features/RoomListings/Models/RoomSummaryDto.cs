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
    double? DistanceInKm,
    string Address = "",
    string? ThumbnailUrl = null,
    IReadOnlyList<string>? Amenities = null,
    DateTimeOffset? CreatedAt = null)
{
    /// <summary>
    /// Converts this search summary DTO to a presentation item model.
    /// </summary>
    public RoomListItemModel ToListItemModel(string? addressOverride = null)
    {
        var resolvedAddress = !string.IsNullOrWhiteSpace(addressOverride)
            ? addressOverride
            : (!string.IsNullOrWhiteSpace(Address) ? Address : "Thái Nguyên");

        return new RoomListItemModel
        {
            Id = Id,
            Title = Title,
            Price = Price,
            AreaSqm = Area,
            Address = resolvedAddress,
            RoomType = RoomType,
            DistanceToCampusKm = DistanceInKm,
            ThumbnailUrl = ThumbnailUrl,
            Amenities = Amenities ?? [],
            CreatedAt = CreatedAt ?? DateTimeOffset.UtcNow,
            IsVerified = IsVerified,
            IsAvailable = true
        };
    }
}
