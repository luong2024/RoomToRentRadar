namespace RoomToRentRadar.Api.DTOs;

/// <summary>
/// Response DTO dùng chung cho mọi API phân trang.
/// App MAUI dùng <see cref="HasNextPage"/> để quyết định có gọi trang tiếp theo khi cuộn hay không.
/// </summary>
public class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int CurrentPage { get; init; }

    public int PageSize { get; init; }

    public int TotalRecords { get; init; }

    public int TotalPages { get; init; }

    public bool HasPreviousPage => CurrentPage > 1;

    public bool HasNextPage => CurrentPage < TotalPages;

    public static PagedResponse<T> Create(IReadOnlyList<T> items, int totalRecords, int pageNumber, int pageSize) => new()
    {
        Items = items,
        TotalRecords = totalRecords,
        CurrentPage = pageNumber,
        PageSize = pageSize,
        TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize)
    };
}
