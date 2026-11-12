using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadar.Shared.Listings;

/// <summary>
/// Page of listings matching the requested filters. <paramref name="ActiveFilters"/>
/// is null when no filter was requested.
/// </summary>
public sealed record ListingSearchResultDto(
    IReadOnlyList<ListingSummaryDto> Data,
    PaginationInfo Pagination,
    int TotalCount,
    bool TotalCountCapped,
    ActiveListingFiltersDto? ActiveFilters)
    : SearchResultDto<ListingSummaryDto>(Data, Pagination, TotalCount, TotalCountCapped);
