using System.Text.RegularExpressions;

namespace RoomToRentRadar.Shared.Dtos;

/// <summary>
/// Quy tắc validate tin đăng (US08 AC1, AC4) — dùng chung cho API (T08.2) và form MAUI (T08.1)
/// để thông báo lỗi hai bên giống hệt nhau. Key lỗi = tên field camelCase trong request.
/// </summary>
public static partial class ListingCreationRules
{
    public const int MinPhotos = 3;
    public const int MaxPhotosPerRequest = 10;
    public const long MaxPhotoSizeBytes = 10L * 1024 * 1024;
    public static readonly IReadOnlyList<string> AllowedPhotoExtensions = [".jpg", ".jpeg", ".png"];

    public const int TitleMinLength = 10;
    public const int TitleMaxLength = 150;
    public const int DescriptionMinLength = 20;
    public const int DescriptionMaxLength = 3000;
    public const decimal MinRent = 300_000m;
    public const decimal MaxRent = 50_000_000m;
    public const double MinArea = 5;
    public const double MaxArea = 300;
    public const int MaxAmenities = 30;
    public const int MaxRuleLength = 300;

    [GeneratedRegex(@"^0\d{9}$")]
    private static partial Regex VietnamPhone();

    /// <summary>
    /// Chuẩn hóa SĐT nhập từ UI: bỏ khoảng trắng / dấu chấm / gạch / ngoặc, đổi tiền tố +84 hoặc 84 thành 0.
    /// Ví dụ "+84 987 000 111" =&gt; "0987000111".
    /// </summary>
    public static string NormalizePhone(string? phone)
    {
        var digits = new string((phone ?? string.Empty).Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (digits.StartsWith("+84", StringComparison.Ordinal)) return "0" + digits[3..];
        if (digits.StartsWith("84", StringComparison.Ordinal) && digits.Length == 11) return "0" + digits[2..];
        return digits;
    }

    /// <summary>
    /// Chuẩn hóa tiện ích: chấp nhận cả item dạng "Wifi, Điều hòa" (form gửi một chuỗi), bỏ trống, bỏ trùng (không phân biệt hoa thường).
    /// </summary>
    public static List<string> NormalizeAmenities(IEnumerable<string>? amenities) =>
        (amenities ?? [])
            .SelectMany(a => (a ?? string.Empty).Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static Dictionary<string, string[]> Validate(CreateListingRequestDto? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["body"] = ["Thiếu thông tin tin đăng."];
            return errors;
        }

        var title = request.Title?.Trim() ?? string.Empty;
        if (title.Length is < TitleMinLength or > TitleMaxLength)
            errors["title"] = [$"Tiêu đề phải từ {TitleMinLength} đến {TitleMaxLength} ký tự."];

        var description = request.Description?.Trim() ?? string.Empty;
        if (description.Length is < DescriptionMinLength or > DescriptionMaxLength)
            errors["description"] = [$"Mô tả phải từ {DescriptionMinLength} đến {DescriptionMaxLength} ký tự."];

        if (request.Rent is < MinRent or > MaxRent)
            errors["rent"] = [$"Giá thuê phải từ {MinRent:N0} đến {MaxRent:N0} VND."];

        if (request.Deposit < 0)
            errors["deposit"] = ["Tiền cọc không được âm."];

        if (double.IsNaN(request.Area) || request.Area is < MinArea or > MaxArea)
            errors["area"] = [$"Diện tích phải từ {MinArea} đến {MaxArea} m²."];

        if (string.IsNullOrWhiteSpace(request.RoomType))
            errors["roomType"] = ["Loại phòng là bắt buộc."];

        if (string.IsNullOrWhiteSpace(request.Address))
            errors["address"] = ["Địa chỉ là bắt buộc."];

        if (double.IsNaN(request.Latitude) || request.Latitude is < -90 or > 90
            || double.IsNaN(request.Longitude) || request.Longitude is < -180 or > 180
            || (request.Latitude == 0 && request.Longitude == 0))
            errors["location"] = ["Vị trí trên bản đồ không hợp lệ."];

        if (NormalizeAmenities(request.Amenities).Count > MaxAmenities)
            errors["amenities"] = [$"Tối đa {MaxAmenities} tiện ích."];

        if (string.IsNullOrWhiteSpace(request.ContactName))
            errors["contactName"] = ["Tên liên hệ là bắt buộc."];

        if (!VietnamPhone().IsMatch(NormalizePhone(request.ContactPhone)))
            errors["contactPhone"] = ["Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0."];

        foreach (var (field, value) in new[]
                 {
                     ("quietHours", request.QuietHours), ("guestPolicy", request.GuestPolicy),
                     ("cookingRules", request.CookingRules), ("parkingRules", request.ParkingRules)
                 })
        {
            if (value is { Length: > MaxRuleLength })
                errors[field] = [$"Tối đa {MaxRuleLength} ký tự."];
        }

        return errors;
    }

    /// <summary>Validate danh sách ảnh theo (tên file, dung lượng byte).</summary>
    public static Dictionary<string, string[]> ValidatePhotos(IReadOnlyList<(string FileName, long Length)> photos)
    {
        var errors = new Dictionary<string, string[]>();
        if (photos.Count < MinPhotos)
        {
            errors["photos"] = [$"Cần ít nhất {MinPhotos} ảnh (hiện có {photos.Count})."];
            return errors;
        }

        if (photos.Count > MaxPhotosPerRequest)
        {
            errors["photos"] = [$"Tối đa {MaxPhotosPerRequest} ảnh cho mỗi lần đăng."];
            return errors;
        }

        var messages = new List<string>();
        foreach (var (fileName, length) in photos)
        {
            var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
            if (!AllowedPhotoExtensions.Contains(ext))
                messages.Add($"'{fileName}': chỉ chấp nhận .jpg, .jpeg, .png.");
            else if (length <= 0)
                messages.Add($"'{fileName}' là tệp rỗng.");
            else if (length > MaxPhotoSizeBytes)
                messages.Add($"'{fileName}' vượt quá {MaxPhotoSizeBytes / (1024 * 1024)} MB.");
        }

        if (messages.Count > 0)
            errors["photos"] = [.. messages];

        return errors;
    }
}
