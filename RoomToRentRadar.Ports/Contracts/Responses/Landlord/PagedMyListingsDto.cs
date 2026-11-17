namespace RoomToRentRadar.Ports.Contracts.Responses.Landlord;

/// <summary>
/// Page of <c>operationId: listMyListings</c> (openapi <c>PagedMyListings</c>, ADR-012 offset paging).
/// <see cref="CountsByStatus"/> always carries one key per <c>ListingStatus</c> name.
/// </summary>
public sealed record PagedMyListingsDto(
    IReadOnlyList<MyListingSummaryDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    IReadOnlyDictionary<string, int> CountsByStatus);
