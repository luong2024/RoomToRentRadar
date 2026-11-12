namespace RoomToRentRadar.Shared.Dtos;

public sealed class PagedResult<T>
{
    public List<T> Items { get; set; } = [];

    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public int TotalCount { get; set; }

    public bool HasNextPage { get; set; }

    public string? ContinuationToken { get; set; }
}
