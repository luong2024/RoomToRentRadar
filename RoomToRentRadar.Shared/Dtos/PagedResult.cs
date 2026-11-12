namespace RoomToRentRadar.Shared.Dtos;

public sealed class PagedResult<T>
{
    public List<T> Items { get; set; } = [];

    public int PageIndex { get; set; } = 1;

    public int CurrentPage
    {
        get => PageIndex;
        set => PageIndex = value;
    }

    public int PageSize { get; set; } = 20;

    public int TotalCount { get; set; }

    public int TotalRecords
    {
        get => TotalCount;
        set => TotalCount = value;
    }

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;

    public bool HasNextPage { get; set; }

    public bool HasPreviousPage => PageIndex > 1;

    public string? ContinuationToken { get; set; }

    public static PagedResult<T> Create(IEnumerable<T> items, int totalRecords, int pageNumber, int pageSize)
    {
        var safePage = Math.Max(1, pageNumber);
        var safeSize = Math.Max(1, pageSize);
        return new PagedResult<T>
        {
            Items = items.ToList(),
            PageIndex = safePage,
            PageSize = safeSize,
            TotalCount = totalRecords,
            HasNextPage = safePage * safeSize < totalRecords
        };
    }
}
