using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RoomToRentRadar.Models.Contracts;

public sealed class GeoJsonPointContract
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Point";

    [JsonPropertyName("coordinates")]
    public double[] Coordinates { get; set; } = [105.8066, 21.5850];
}

public sealed class PhotoContract
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("is360")]
    public bool Is360 { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }
}

public sealed class HouseRulesContract
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("quietHours")]
    public string QuietHours { get; set; } = string.Empty;

    [JsonPropertyName("guestPolicy")]
    public string GuestPolicy { get; set; } = string.Empty;

    [JsonPropertyName("cookingRules")]
    public string CookingRules { get; set; } = string.Empty;

    [JsonPropertyName("parkingRules")]
    public string ParkingRules { get; set; } = string.Empty;
}

public sealed class LandlordContract
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("phone")]
    public string Phone { get; set; } = string.Empty;
}

/// <summary>
/// Contract khớp 1-1 với ListingDetailDto từ GET /api/v1/listings/{id} (US02 / T02.1).
/// </summary>
public sealed class ListingDetailContract
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("campusId")]
    public string CampusId { get; set; } = "ictu-main";

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Available";

    [JsonPropertyName("isVerified")]
    public bool IsVerified { get; set; }

    [JsonPropertyName("isAvailable")]
    public bool IsAvailable { get; set; }

    [JsonPropertyName("unavailableMessage")]
    public string? UnavailableMessage { get; set; }

    [JsonPropertyName("rent")]
    public decimal Rent { get; set; }

    [JsonPropertyName("deposit")]
    public decimal Deposit { get; set; }

    [JsonPropertyName("area")]
    public double Area { get; set; }

    [JsonPropertyName("roomType")]
    public string RoomType { get; set; } = "Studio";

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public GeoJsonPointContract? Location { get; set; }

    [JsonPropertyName("amenities")]
    public List<string> Amenities { get; set; } = [];

    [JsonPropertyName("securityScore")]
    public double SecurityScore { get; set; }

    [JsonPropertyName("photos")]
    public List<PhotoContract> Photos { get; set; } = [];

    [JsonPropertyName("houseRules")]
    public HouseRulesContract? HouseRules { get; set; }

    [JsonPropertyName("landlord")]
    public LandlordContract? Landlord { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("similarRooms")]
    public List<ListingCardDto> SimilarRooms { get; set; } = [];
}

public sealed class SearchActiveFiltersContract
{
    [JsonPropertyName("campusId")]
    public string CampusId { get; set; } = "ictu-main";

    [JsonPropertyName("minRent")]
    public decimal? MinRent { get; set; }

    [JsonPropertyName("maxRent")]
    public decimal? MaxRent { get; set; }

    [JsonPropertyName("minArea")]
    public double? MinArea { get; set; }

    [JsonPropertyName("maxArea")]
    public double? MaxArea { get; set; }

    [JsonPropertyName("roomType")]
    public string? RoomType { get; set; }

    [JsonPropertyName("maxDistanceMeters")]
    public double? MaxDistanceMeters { get; set; }

    [JsonPropertyName("sortBy")]
    public string SortBy { get; set; } = "distance";
}

/// <summary>
/// Query gửi lên GET /api/v1/listings/search (US04 / T04.1).
/// </summary>
public sealed class SearchListingsQueryContract
{
    public string CampusId { get; set; } = "ictu-main";

    public decimal? MinRent { get; set; }

    public decimal? MaxRent { get; set; }

    public double? MinArea { get; set; }

    public double? MaxArea { get; set; }

    public string? RoomType { get; set; }

    public double? MaxDistanceMeters { get; set; }

    public string SortBy { get; set; } = "distance";

    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Contract khớp 1-1 với SearchResponseDto từ GET /api/v1/listings/search (US04 / T04.1).
/// </summary>
public sealed class SearchListingsResponseContract
{
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("activeFilters")]
    public SearchActiveFiltersContract ActiveFilters { get; set; } = new();

    [JsonPropertyName("items")]
    public List<ListingCardDto> Items { get; set; } = [];

    [JsonPropertyName("pageIndex")]
    public int PageIndex { get; set; } = 1;

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 20;

    [JsonPropertyName("hasNextPage")]
    public bool HasNextPage { get; set; }
}
