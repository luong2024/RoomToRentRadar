using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services.Mapping;
using RoomToRentRadar.Api.Services.Search;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services;

public sealed class ListingSearchService(IListingRepository repository) : IListingSearchService
{
    public async Task<SearchResponseDto> SearchAsync(
        ListingSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await repository.SearchAsync(query, cancellationToken);
        var paging = ListingPaging.From(query);

        return new SearchResponseDto
        {
            TotalCount = totalCount,
            PageIndex = paging.PageIndex,
            PageSize = paging.PageSize,
            HasNextPage = paging.HasNextPage(totalCount),
            ActiveFilters = ToActiveFilters(query),
            Items = items.Select(ListingCardMapper.ToCard).ToList()
        };
    }

    private static SearchActiveFiltersDto ToActiveFilters(ListingSearchQuery query) => new()
    {
        CampusId = query.CampusId,
        MinRent = query.MinRent,
        MaxRent = query.MaxRent,
        MinArea = query.MinArea,
        MaxArea = query.MaxArea,
        RoomType = query.RoomType,
        MaxDistanceMeters = query.MaxDistanceMeters,
        SortBy = string.IsNullOrWhiteSpace(query.SortBy) ? ListingSortKeys.Default : query.SortBy
    };
}
