namespace RoomToRentRadar.Shared.Dtos;

public sealed class SearchActiveFiltersDto
{
    public string CampusId { get; set; } = "ictu-main";

    public decimal? MinRent { get; set; }

    public decimal? MaxRent { get; set; }

    public double? MinArea { get; set; }

    public double? MaxArea { get; set; }

    public string? RoomType { get; set; }

    public double? MaxDistanceMeters { get; set; }

    public string SortBy { get; set; } = "distance";
}
