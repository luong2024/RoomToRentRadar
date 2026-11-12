using System.ComponentModel.DataAnnotations;

namespace RoomToRentRadar.Shared.Dtos;

public sealed class ListingSearchQuery
{
    public string CampusId { get; set; } = "ictu-main";

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

    public string SortBy { get; set; } = "distance";

    [Range(1, 50)]
    public int PageSize { get; set; } = 20;

    [Range(1, int.MaxValue)]
    public int PageIndex { get; set; } = 1;
}
