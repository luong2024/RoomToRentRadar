namespace RoomToRentRadar.Models.RoomListings;

/// <summary>
/// Filter criteria for querying room listings by price range, room type, and selected amenities.
/// </summary>
public sealed record RoomFilterCriteria
{
    public decimal? MinPrice { get; init; }

    public decimal? MaxPrice { get; init; }

    public string? RoomType { get; init; }

    public IReadOnlyList<string> Amenities { get; init; } = [];

    public bool HasActiveFilters =>
        MinPrice.HasValue ||
        MaxPrice.HasValue ||
        !string.IsNullOrWhiteSpace(RoomType) ||
        Amenities.Count > 0;
}
