using System.Globalization;
using System.Text;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Features.ListingManagement.Models;

/// <summary>Parsed values ready to be sent to the listing endpoints.</summary>
public sealed record ListingFormValues(
    long Price,
    long Deposit,
    double AreaM2,
    int Bedrooms,
    double Lat,
    double Lng);

/// <summary>
/// Client-side validation for the listing editor (US08 AC1 and AC4). Pure code
/// with no MAUI dependencies so the rules stay unit-testable.
/// </summary>
public static class ListingFormValidator
{
    public const double MinimumLat = -90d;
    public const double MaximumLat = 90d;
    public const double MinimumLng = -180d;
    public const double MaximumLng = 180d;

    /// <summary>
    /// Returns field key (see <see cref="ListingFieldKeys"/>) to a Vietnamese
    /// message. An empty dictionary means the form can be submitted.
    /// </summary>
    public static Dictionary<string, string> Validate(ListingFormState state, bool isCreate)
    {
        ArgumentNullException.ThrowIfNull(state);

        var errors = new Dictionary<string, string>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(state.Title))
            errors[ListingFieldKeys.Title] = "Vui lòng nhập tiêu đề tin đăng.";

        if (string.IsNullOrWhiteSpace(state.Address))
            errors[ListingFieldKeys.Address] = "Vui lòng nhập địa chỉ phòng trọ.";

        if (string.IsNullOrWhiteSpace(state.PriceText))
            errors[ListingFieldKeys.Price] = "Vui lòng nhập giá thuê.";
        else if (!TryParseAmount(state.PriceText, out var price))
            errors[ListingFieldKeys.Price] = "Giá thuê không hợp lệ.";
        else if (price <= 0)
            errors[ListingFieldKeys.Price] = "Giá thuê phải lớn hơn 0.";

        if (string.IsNullOrWhiteSpace(state.DepositText))
            errors[ListingFieldKeys.Deposit] = "Vui lòng nhập tiền cọc.";
        else if (!TryParseAmount(state.DepositText, out var deposit))
            errors[ListingFieldKeys.Deposit] = "Tiền cọc không hợp lệ.";
        else if (deposit < 0)
            errors[ListingFieldKeys.Deposit] = "Tiền cọc phải lớn hơn hoặc bằng 0.";

        if (string.IsNullOrWhiteSpace(state.AreaText))
            errors[ListingFieldKeys.Area] = "Vui lòng nhập diện tích.";
        else if (!TryParseDecimal(state.AreaText, out var area))
            errors[ListingFieldKeys.Area] = "Diện tích không hợp lệ.";
        else if (area <= 0)
            errors[ListingFieldKeys.Area] = "Diện tích phải lớn hơn 0 m².";

        if (state.RoomType is null)
            errors[ListingFieldKeys.RoomType] = "Vui lòng chọn loại phòng.";

        if (!TryParseBedrooms(state.BedroomsText, out _))
            errors[ListingFieldKeys.Bedrooms] = "Số phòng ngủ không hợp lệ.";

        if (string.IsNullOrWhiteSpace(state.LatText))
            errors[ListingFieldKeys.Lat] = "Vui lòng nhập vĩ độ.";
        else if (!TryParseCoordinate(state.LatText, MinimumLat, MaximumLat, out _))
            errors[ListingFieldKeys.Lat] = "Vĩ độ phải là số trong khoảng -90 đến 90.";

        if (string.IsNullOrWhiteSpace(state.LngText))
            errors[ListingFieldKeys.Lng] = "Vui lòng nhập kinh độ.";
        else if (!TryParseCoordinate(state.LngText, MinimumLng, MaximumLng, out _))
            errors[ListingFieldKeys.Lng] = "Kinh độ phải là số trong khoảng -180 đến 180.";

        var photoErrors = ValidatePhotos(state, isCreate);
        if (photoErrors is { } message)
            errors[ListingFieldKeys.Photos] = message;

        return errors;
    }

    /// <summary>
    /// Parses the scalar fields after <see cref="Validate"/> reported no errors.
    /// Returns false when the text still cannot be interpreted.
    /// </summary>
    public static bool TryReadValues(ListingFormState state, out ListingFormValues values)
    {
        ArgumentNullException.ThrowIfNull(state);

        values = null!;

        if (!TryParseAmount(state.PriceText, out var price)
            || !TryParseAmount(state.DepositText, out var deposit)
            || !TryParseDecimal(state.AreaText, out var area)
            || !TryParseBedrooms(state.BedroomsText, out var bedrooms)
            || !TryParseCoordinate(state.LatText, MinimumLat, MaximumLat, out var lat)
            || !TryParseCoordinate(state.LngText, MinimumLng, MaximumLng, out var lng))
        {
            return false;
        }

        values = new ListingFormValues(price, deposit, area, bedrooms, lat, lng);
        return true;
    }

    /// <summary>
    /// Whole đồng amounts. Group separators (<c>.</c>, <c>,</c>, spaces) are
    /// ignored so "3.500.000" and "3500000" both read as 3500000.
    /// </summary>
    public static bool TryParseAmount(string? text, out long value)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        var digits = new StringBuilder(text.Length);

        foreach (var character in text.Trim())
        {
            if (char.IsAsciiDigit(character))
            {
                digits.Append(character);
                continue;
            }

            if (character is '.' or ',' or ' ' or '\u00A0' or '_')
                continue;

            return false;
        }

        if (digits.Length is 0 or > 18)
            return false;

        return long.TryParse(
            digits.ToString(),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out value);
    }

    /// <summary>Accepts "." or "," as the decimal separator.</summary>
    public static bool TryParseDecimal(string? text, out double value)
    {
        value = double.NaN;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = text.Trim().Replace(',', '.');

        return double.TryParse(
            normalized,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value)
            && double.IsFinite(value);
    }

    public static bool TryParseBedrooms(string? text, out int value)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(text))
            return true;

        return int.TryParse(
                   text.Trim(),
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out value)
               && value >= 0;
    }

    public static bool TryParseCoordinate(
        string? text,
        double minimum,
        double maximum,
        out double value)
    {
        value = double.NaN;

        if (!TryParseDecimal(text, out var parsed))
            return false;

        if (parsed < minimum || parsed > maximum)
            return false;

        value = parsed;
        return true;
    }

    private static string? ValidatePhotos(ListingFormState state, bool isCreate)
    {
        var newPhotos = state.NewPhotos ?? [];

        if (newPhotos.Count > ListingPhotoRules.MaxPhotosPerRequest)
            return $"Chỉ được thêm tối đa {ListingPhotoRules.MaxPhotosPerRequest} ảnh mỗi lần lưu.";

        if (isCreate && state.ExistingPhotoCount + newPhotos.Count < ListingPhotoRules.MinPhotosOnCreate)
            return $"Cần ít nhất {ListingPhotoRules.MinPhotosOnCreate} ảnh (JPG hoặc PNG) để gửi duyệt.";

        foreach (var photo in newPhotos)
        {
            if (!ListingPhotoRules.IsAllowedType(photo.ContentType, photo.FileName))
                return $"Ảnh \"{photo.FileName}\" phải là JPG hoặc PNG.";

            if (!ListingPhotoRules.IsWithinSizeLimit(photo.Length))
                return $"Ảnh \"{photo.FileName}\" vượt quá 10MB.";
        }

        return null;
    }
}
