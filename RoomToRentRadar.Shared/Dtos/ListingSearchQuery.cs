using System.ComponentModel.DataAnnotations;

namespace RoomToRentRadar.Shared.Dtos;

public sealed class ListingSearchQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    public string CampusId { get; set; } = CampusIds.Default;

    [Range(0, double.MaxValue)]
    public decimal? MinRent { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MaxRent { get; set; }

    [Range(0, double.MaxValue)]
    public double? MinArea { get; set; }

    [Range(0, double.MaxValue)]
    public double? MaxArea { get; set; }

    public string? RoomType { get; set; }

    [Range(0, double.MaxValue)]
    public double? MaxDistanceMeters { get; set; }

    /// <summary>Xem <see cref="ListingSortKeys"/>.</summary>
    public string SortBy { get; set; } = ListingSortKeys.Default;

    [Range(1, MaxPageSize)]
    public int PageSize { get; set; } = DefaultPageSize;

    [Range(1, int.MaxValue)]
    public int PageIndex { get; set; } = 1;
}
