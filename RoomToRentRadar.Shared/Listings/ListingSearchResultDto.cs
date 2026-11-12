using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadar.Shared.Listings;

/// <summary>
/// Page of listings matching the requested filters. <paramref name="TotalCount"/> is
/// the number of matches before the cursor is applied, capped at
/// <see cref="TotalCountLimit"/>; <paramref name="ActiveFilters"/> is null when no
/// filter was requested.
/// </summary>
public sealed record ListingSearchResultDto(
    IReadOnlyList<ListingSummaryDto> Data,
    PaginationInfo Pagination,
    int TotalCount,
    bool TotalCountCapped,
    ActiveListingFiltersDto? ActiveFilters)
{
    /// <summary>Upper bound reported by <c>totalCount</c>.</summary>
    public const int TotalCountLimit = 1000;
}
