using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services.Search;

/// <summary>Chuẩn hóa phân trang một chỗ để repository và service không tự clamp mỗi nơi một kiểu.</summary>
public readonly record struct ListingPaging(int PageIndex, int PageSize)
{
    public int Skip => (PageIndex - 1) * PageSize;

    public bool HasNextPage(int totalCount) => PageIndex * PageSize < totalCount;

    public static ListingPaging From(ListingSearchQuery query) => new(
        Math.Max(1, query.PageIndex),
        Math.Clamp(query.PageSize, 1, ListingSearchQuery.MaxPageSize));
}
