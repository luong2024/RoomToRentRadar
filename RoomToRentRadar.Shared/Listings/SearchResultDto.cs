using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadar.Shared.Listings;

/// <summary>
/// Shared page envelope for listing searches. <paramref name="TotalCount"/> is the
/// number of matches before the cursor is applied, capped at
/// <see cref="SearchLimits.TotalCountLimit"/>.
/// </summary>
public record SearchResultDto<T>(
    IReadOnlyList<T> Data,
    PaginationInfo Pagination,
    int TotalCount,
    bool TotalCountCapped);

/// <summary>Limits shared by every listing search endpoint.</summary>
public static class SearchLimits
{
    /// <summary>Upper bound reported by <c>totalCount</c>.</summary>
    public const int TotalCountLimit = 1000;
}
