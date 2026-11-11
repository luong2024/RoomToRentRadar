namespace RoomToRentRadar.Api.DTOs;

/// <summary>Dữ liệu rút gọn của một phòng trọ trả về cho app (card trong danh sách).</summary>
public record ListingDto(
    string Id,
    string Title,
    decimal Price,
    double Area,
    string Address,
    string District,
    string City,
    string? ThumbnailUrl,
    bool IsAvailable,
    DateTime CreatedAt);
