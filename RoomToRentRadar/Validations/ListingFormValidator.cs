using System.Globalization;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Validations;

public sealed record ListingFormFields(string Title, string Address, string Rent, string Deposit,
    string Area, RoomType? RoomType, string Latitude, string Longitude, string Description);

public static class ListingFormValidator
{
    public static IReadOnlyDictionary<string, string> Validate(ListingFormFields fields, int photoCount)
    {
        var errors = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(fields.Title)) errors["title"] = "Nhập tiêu đề tin đăng.";
        else if (fields.Title.Trim().Length > 200) errors["title"] = "Tiêu đề tối đa 200 ký tự.";
        if (string.IsNullOrWhiteSpace(fields.Address)) errors["address"] = "Nhập địa chỉ phòng.";
        else if (fields.Address.Trim().Length > 300) errors["address"] = "Địa chỉ tối đa 300 ký tự.";
        if (fields.Description.Trim().Length > 4000) errors["description"] = "Mô tả tối đa 4000 ký tự.";
        if (!TryMoney(fields.Rent, out var rent) || rent <= 0) errors["price"] = "Nhập tiền thuê nguyên đồng, lớn hơn 0.";
        if (!TryMoney(fields.Deposit, out var deposit) || deposit < 0) errors["deposit"] = "Nhập tiền cọc nguyên đồng, từ 0 trở lên.";
        if (!TryNumber(fields.Area, out var area) || area <= 0) errors["area_m2"] = "Nhập diện tích lớn hơn 0.";
        if (fields.RoomType is null || !Enum.IsDefined(fields.RoomType.Value)) errors["room_type"] = "Chọn loại phòng.";
        if (!TryNumber(fields.Latitude, out var latitude) || latitude is < -90 or > 90) errors["lat"] = "Chọn vị trí trên bản đồ hoặc nhập vĩ độ hợp lệ.";
        if (!TryNumber(fields.Longitude, out var longitude) || longitude is < -180 or > 180) errors["lng"] = "Chọn vị trí trên bản đồ hoặc nhập kinh độ hợp lệ.";
        if (photoCount < 3) errors["photos"] = "Cần ít nhất 3 ảnh hợp lệ.";
        if (photoCount > 20) errors["photos"] = "Mỗi tin có tối đa 20 ảnh.";
        return errors;
    }

    public static bool TryMoney(string value, out long result)
        => long.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out result);

    public static bool TryNumber(string value, out double result)
        => double.TryParse(value.Trim().Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out result) && double.IsFinite(result);
}
