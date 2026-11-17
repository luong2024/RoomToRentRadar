namespace RoomToRentRadar.Ports.Contracts.Responses.Listings;

using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Common;

/// <summary>Matches <c>PagedMyListings</c> in <c>docs/api/openapi.yaml</c> (PageMeta + items + countsByStatus).</summary>
public sealed record PagedMyListings(
    IReadOnlyList<MyListingSummaryDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    IReadOnlyDictionary<ListingStatus, int> CountsByStatus
) : PagedResult<MyListingSummaryDto>(Items, TotalCount, PageNumber, PageSize);