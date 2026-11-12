using System.ComponentModel.DataAnnotations;

namespace RoomToRentRadar.Shared.Dtos;

/// <summary>Request DTO cho GET /api/v1/listings (T01.1 - phân trang &amp; sắp xếp).</summary>
public sealed class ListingQueryRequest
{
    public const int MaxPageSize = 50;

    public string CampusId { get; set; } = "ictu-main";

    [Range(1, int.MaxValue)]
    public int PageNumber { get; set; } = 1;

    /// <summary>Tương thích với client MAUI truyền pageIndex.</summary>
    [Range(1, int.MaxValue)]
    public int? PageIndex { get; set; }

    [Range(1, MaxPageSize)]
    public int PageSize { get; set; } = 10;

    /// <summary>Tiêu chí sắp xếp: distance (mặc định), rent/price, area, updatedAt.</summary>
    public string SortBy { get; set; } = "distance";

    public bool IsAscending { get; set; } = true;

    public string? ContinuationToken { get; set; }

    public int EffectivePage => Math.Max(1, PageIndex ?? PageNumber);
}
