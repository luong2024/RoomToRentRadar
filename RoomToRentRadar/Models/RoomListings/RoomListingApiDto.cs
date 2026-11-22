namespace RoomToRentRadar.Models.RoomListings;

/// <summary>
/// Client-side DTO representing a room listing returned by GET api/v1/rooms.
/// Decoupled from backend assembly references.
/// </summary>
public sealed class RoomListingApiDto
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public decimal MonthlyRent { get; set; }

    public decimal? Price { get; set; }

    public string Currency { get; set; } = "VND";

    public double AreaSquareMeters { get; set; }

    public double? AreaSqm { get; set; }

    public double? Area { get; set; }

    public double DistanceToCampusKm { get; set; }

    public bool IsVerified { get; set; }

    public bool? IsAvailable { get; set; }

    public string Address { get; set; } = string.Empty;

    public string? ThumbnailUrl { get; set; }

    public string CampusId { get; set; } = "campus-ictu";

    public string? RoomType { get; set; }

    public string? District { get; set; }

    public List<string>? Amenities { get; set; }
}
