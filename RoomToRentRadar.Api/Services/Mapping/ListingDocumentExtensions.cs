using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services.Mapping;

/// <summary>
/// Các quy tắc đọc dùng chung trên <see cref="ListingDocument"/> để không lặp lại chuỗi "Available"
/// và logic chọn ảnh bìa ở từng service (DRY).
/// </summary>
public static class ListingDocumentExtensions
{
    public static bool IsAvailable(this ListingDocument listing) =>
        string.Equals(listing.Status, ListingAvailabilityStatus.Available, StringComparison.OrdinalIgnoreCase);

    /// <summary>Tin hiển thị công khai: đang trống và đã được xác minh.</summary>
    public static bool IsPublic(this ListingDocument listing) => listing.IsAvailable() && listing.IsVerified;

    /// <summary>URL ảnh gốc đầu tiên theo thứ tự hiển thị (rỗng nếu chưa có ảnh).</summary>
    public static string CoverPhotoUrl(this ListingDocument listing) =>
        listing.Photos.OrderBy(p => p.Order).Select(p => p.Url).FirstOrDefault() ?? string.Empty;

    /// <summary>Thumbnail của ảnh đầu tiên; fallback về ảnh gốc khi chưa có thumbnail.</summary>
    public static string CoverThumbnailUrl(this ListingDocument listing) =>
        listing.Photos
            .OrderBy(p => p.Order)
            .Select(p => p.ThumbnailUrl is { Length: > 0 } thumbnail ? thumbnail : p.Url)
            .FirstOrDefault() ?? string.Empty;
}
