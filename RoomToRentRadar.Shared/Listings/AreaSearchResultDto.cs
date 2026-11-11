using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadar.Shared.Listings;

/// <summary>
/// Page of exact polygon matches. <paramref name="TotalCount"/> is the number of
/// matches before the cursor is applied, capped at <see cref="TotalCountLimit"/>.
/// </summary>
public sealed record AreaSearchResultDto(
    IReadOnlyList<ListingSummaryDto> Data,
    PaginationInfo Pagination,
    int TotalCount,
    bool TotalCountCapped)
{
    /// <summary>Upper bound reported by <c>totalCount</c>.</summary>
    public const int TotalCountLimit = 1000;
}
