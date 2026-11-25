namespace RoomToRentRadar.Shared.Dtos;

/// <summary>
/// Nhãn tiếng Việt cho badge trạng thái (T08.1 <c>PendingReviewBadge</c>).
/// Giá trị trạng thái dùng chung hằng số <see cref="ListingAvailabilityStatus"/> / <see cref="ListingModerationStatus"/> của T08.3.
/// </summary>
public static class ListingStatusLabels
{
    public static string ToLabel(string status) => status switch
    {
        ListingAvailabilityStatus.PendingReview => "Đang chờ duyệt (Pending Review)",
        ListingAvailabilityStatus.Available => "Đang hiển thị",
        ListingAvailabilityStatus.Rented => "Đã cho thuê",
        ListingAvailabilityStatus.Rejected => "Bị từ chối",
        ListingAvailabilityStatus.Hidden => "Đang ẩn",
        ListingAvailabilityStatus.Archived => "Đã xóa",
        _ => status
    };
}

/// <summary>
/// Các field text của POST /api/v1/landlord/listings (US08 / T08.2), gửi dạng multipart/form-data
/// cùng với các file <c>photos</c> (&gt;= 3 ảnh JPG/PNG &lt;= 10MB) và cờ <c>is360</c> theo cùng thứ tự.
/// Tọa độ nhận theo (latitude, longitude) như Google Maps; server tự đổi sang GeoJSON [lng, lat].
/// </summary>
public sealed class CreateListingRequestDto
{
    public string CampusId { get; set; } = "ictu-main";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Rent { get; set; }
    public decimal Deposit { get; set; }
    public double Area { get; set; }
    public string RoomType { get; set; } = "Studio";
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public List<string> Amenities { get; set; } = [];

    public string ContactName { get; set; } = string.Empty;

    /// <summary>Số di động VN 10 số, bắt đầu bằng 0.</summary>
    public string ContactPhone { get; set; } = string.Empty;

    public string? QuietHours { get; set; }
    public string? GuestPolicy { get; set; }
    public string? CookingRules { get; set; }
    public string? ParkingRules { get; set; }
}

/// <summary>Tin đăng nhìn từ phía chủ trọ (mọi trạng thái, không che SĐT).</summary>
public sealed class LandlordListingDto
{
    public string Id { get; set; } = string.Empty;
    public string CampusId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>PendingReview | Available | Rented | Rejected — xem <see cref="ListingStatuses"/>.</summary>
    public string Status { get; set; } = string.Empty;

    public string StatusLabel { get; set; } = string.Empty;
    public string ModerationStatus { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public decimal Rent { get; set; }
    public decimal Deposit { get; set; }
    public double Area { get; set; }
    public string RoomType { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double DistanceMeters { get; set; }
    public List<string> Amenities { get; set; } = [];
    public string ContactName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public int PhotoCount { get; set; }
    public string CoverPhotoUrl { get; set; } = string.Empty;
    public List<RoomToRentRadar.Shared.Photos.PhotoDto> Photos { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
