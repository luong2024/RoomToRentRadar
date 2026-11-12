using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RoomToRentRadar.Models.Contracts;

/// <summary>
/// DTO tạm tương thích với contract GET /api/v1/listings (T01.1).
/// Khi RoomToRentRadar.Shared được Hiếu đẩy lên Group-3, chỉ cần thay namespace bằng Shared.
/// </summary>
public sealed class ListingCardDto
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

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Available";

    [JsonPropertyName("isVerified")]
    public bool IsVerified { get; set; }
}

public sealed class PagedResult<T>
{
    [JsonPropertyName("items")]
    public List<T> Items { get; set; } = [];

    [JsonPropertyName("pageIndex")]
    public int PageIndex { get; set; } = 1;

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 20;

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("hasNextPage")]
    public bool HasNextPage { get; set; }

    [JsonPropertyName("continuationToken")]
    public string? ContinuationToken { get; set; }
}