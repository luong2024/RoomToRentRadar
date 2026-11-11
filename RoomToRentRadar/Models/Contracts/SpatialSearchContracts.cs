using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RoomToRentRadar.Models.Contracts;

/// <summary>
/// Request gửi lên POST /api/v1/listings/spatial-search (US05 / T05.2).
/// GeoJSON yêu cầu thứ tự [longitude, latitude] và điểm cuối trùng điểm đầu.
/// </summary>
public sealed class SpatialSearchRequest
{
    [JsonPropertyName("polygon")]
    public List<double[]> Polygon { get; set; } = [];

    [JsonPropertyName("sortBy")]
    public string SortBy { get; set; } = "distance";
}

/// <summary>
/// DTO thẻ ghim trả về cho T05.1 & chuẩn bị tọa độ sẵn cho T05.3.
/// </summary>
public sealed class SpatialSearchPinDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("campusId")]
    public string CampusId { get; set; } = "ictu-main";

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("rent")]
    public decimal Rent { get; set; }

    [JsonPropertyName("area")]
    public double Area { get; set; }

    [JsonPropertyName("distanceMeters")]
    public double DistanceMeters { get; set; }

    [JsonPropertyName("roomType")]
    public string RoomType { get; set; } = "Phòng trọ";

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("thumbnailUrl")]
    public string ThumbnailUrl { get; set; } = string.Empty;

    [JsonPropertyName("isVerified")]
    public bool IsVerified { get; set; }

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }
}

public sealed class SpatialSearchResponse
{
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("items")]
    public List<SpatialSearchPinDto> Items { get; set; } = [];
}