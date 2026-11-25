using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RoomToRentRadar.Models.Contracts;

/// <summary>
/// DTO đồng bộ danh sách yêu thích với Backend T06.1 (US06).
/// </summary>
public sealed class FavouriteToggleResponse
{
    [JsonPropertyName("listingId")]
    public string ListingId { get; set; } = string.Empty;

    [JsonPropertyName("isFavourite")]
    public bool IsFavourite { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class FavouritesListResponse
{
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("items")]
    public List<ListingCardDto> Items { get; set; } = [];
}