using System.ComponentModel.DataAnnotations;

namespace RoomToRentRadar.Api.DTOs;

/// <summary>
/// Request DTO cho GET /api/listings (bind từ query string).
/// Ví dụ: /api/listings?pageNumber=2&amp;pageSize=10&amp;sortBy=price&amp;isAscending=true
/// </summary>
public class ListingQueryParameters
{
    public const int MaxPageSize = 50;

    /// <summary>Số trang, bắt đầu từ 1.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "PageNumber phải >= 1.")]
    public int PageNumber { get; set; } = 1;

    /// <summary>Số bản ghi mỗi trang (1 - 50).</summary>
    [Range(1, MaxPageSize, ErrorMessage = "PageSize phải nằm trong khoảng 1 - 50.")]
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Tiêu chí sắp xếp: "price" | "area" | "createdAt" (mặc định).
    /// Giá trị không hợp lệ sẽ được sắp xếp theo "createdAt".
    /// </summary>
    public string? SortBy { get; set; } = ListingSortFields.CreatedAt;

    /// <summary>true = tăng dần, false = giảm dần (mặc định).</summary>
    public bool IsAscending { get; set; } = false;
}

/// <summary>Whitelist các trường được phép sắp xếp.</summary>
public static class ListingSortFields
{
    public const string Price = "price";
    public const string Area = "area";
    public const string CreatedAt = "createdAt";
}
