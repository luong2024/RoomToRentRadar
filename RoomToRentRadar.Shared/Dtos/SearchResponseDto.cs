namespace RoomToRentRadar.Shared.Dtos;

public sealed class SearchResponseDto
{
    public int TotalCount { get; set; }

    public SearchActiveFiltersDto ActiveFilters { get; set; } = new();

    public List<ListingCardDto> Items { get; set; } = [];

    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public bool HasNextPage { get; set; }
}