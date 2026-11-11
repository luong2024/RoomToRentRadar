namespace RoomToRentRadar.Features.RoomListings.Models;

/// <summary>
/// Paginated response contract returned by the room listings API.
/// </summary>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public bool HasMorePages => Page > 0 && PageSize > 0 && (Page * PageSize) < TotalCount;
}
