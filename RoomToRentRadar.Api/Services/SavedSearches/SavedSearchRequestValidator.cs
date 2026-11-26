using RoomToRentRadar.Api.Services.Geo;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services.SavedSearches;

/// <summary>Validate request lưu tìm kiếm: tên, bộ lọc, polygon — gom mọi lỗi rồi ném một lần.</summary>
public static class SavedSearchRequestValidator
{
    public const int MaxNameLength = 100;

    /// <summary>Validate request; trả về polygon đã chuẩn hóa (đóng ring) hoặc null nếu không vẽ vùng.</summary>
    /// <exception cref="RequestValidationException">Request không hợp lệ.</exception>
    public static IReadOnlyList<double[]>? Validate(SaveSearchRequestDto? request)
    {
        if (request is null)
        {
            throw RequestValidationException.For("body", "Thiếu dữ liệu tìm kiếm cần lưu.");
        }

        var errors = new Dictionary<string, string[]>();
        ValidateName(request.Name, errors);
        ValidateFilters(request.Filters ?? new SavedSearchFiltersDto(), errors);
        var polygon = ValidatePolygon(request.Polygon, errors);

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        return polygon;
    }

    /// <summary>Tên để trống được chấp nhận: server tự đặt tên (xem <see cref="SavedSearchService.DefaultName"/>).</summary>
    private static void ValidateName(string? name, Dictionary<string, string[]> errors)
    {
        if ((name?.Trim().Length ?? 0) > MaxNameLength)
        {
            errors["name"] = [$"Tên tìm kiếm tối đa {MaxNameLength} ký tự."];
        }
    }

    private static void ValidateFilters(SavedSearchFiltersDto filters, Dictionary<string, string[]> errors)
    {
        if (filters.MinRent < 0 || filters.MaxRent < 0) errors["filters.rent"] = ["Giá thuê không được âm."];
        else if (filters.MinRent > filters.MaxRent) errors["filters.rent"] = ["MinRent phải nhỏ hơn hoặc bằng MaxRent."];

        if (filters.MinArea < 0 || filters.MaxArea < 0) errors["filters.area"] = ["Diện tích không được âm."];
        else if (filters.MinArea > filters.MaxArea) errors["filters.area"] = ["MinArea phải nhỏ hơn hoặc bằng MaxArea."];

        if (filters.MaxDistanceMeters <= 0) errors["filters.maxDistanceMeters"] = ["Khoảng cách tối đa phải lớn hơn 0."];
    }

    /// <summary>Polygon là tùy chọn; chỉ validate khi client có gửi.</summary>
    private static IReadOnlyList<double[]>? ValidatePolygon(IReadOnlyList<double[]>? polygon, Dictionary<string, string[]> errors)
    {
        if (polygon is not { Count: > 0 })
        {
            return null;
        }

        if (GeoPolygon.TryNormalize(polygon, out var ring, out var error))
        {
            return ring;
        }

        errors[GeoPolygon.FieldName] = [error];
        return null;
    }
}
