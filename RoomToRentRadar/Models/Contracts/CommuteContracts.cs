using System.Text.Json.Serialization;

namespace RoomToRentRadar.Models.Contracts;

/// <summary>
/// Cơ sở trường đích để tính thời gian di chuyển từ phòng trọ (US09 / T09.2).
/// </summary>
public sealed record CampusDestinationOption(
    string CampusId,
    string CampusName,
    double Latitude,
    double Longitude)
{
    public override string ToString() => CampusName;
}

/// <summary>
/// DTO khớp contract ước tính thời gian di chuyển từ Backend T09.1 (GET /api/v1/listings/{listingId}/commute).
/// </summary>
public sealed class CommuteEstimateDto
{
    [JsonPropertyName("listingId")]
    public string ListingId { get; set; } = string.Empty;

    [JsonPropertyName("campusId")]
    public string CampusId { get; set; } = "ictu-main";

    [JsonPropertyName("campusName")]
    public string CampusName { get; set; } = "Cổng chính ICTU";

    [JsonPropertyName("motorbikeMinutes")]
    public int MotorbikeMinutes { get; set; }

    [JsonPropertyName("busMinutes")]
    public int BusMinutes { get; set; }

    [JsonPropertyName("walkMinutes")]
    public int WalkMinutes { get; set; }

    [JsonPropertyName("routeDistanceKm")]
    public double RouteDistanceKm { get; set; }

    [JsonPropertyName("isFallbackStraightLine")]
    public bool IsFallbackStraightLine { get; set; }

    [JsonPropertyName("straightLineKm")]
    public double StraightLineKm { get; set; }
}
