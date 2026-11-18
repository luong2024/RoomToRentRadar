namespace RoomToRentRadar.Features.RoomListings.Models;

/// <summary>
/// Result contract returned by GET api/rooms search endpoint.
/// </summary>
public sealed record SearchRoomsResult(
    IReadOnlyList<RoomSummaryDto> Items,
    int TotalCount,
    int TotalPages,
    int CurrentPage,
    IReadOnlyList<string> ActiveFilters)
{
    public bool HasMorePages => CurrentPage > 0 && CurrentPage < TotalPages;
}
