using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services.SavedSearches;

/// <summary>Map document ↔ DTO cho saved search (tách khỏi service để service chỉ lo luồng nghiệp vụ).</summary>
public static class SavedSearchMapper
{
    /// <param name="today">Ngày hiện tại giờ VN — bộ đếm alert của ngày khác được hiển thị là 0.</param>
    public static SavedSearchDto ToDto(SavedSearchDocument search, DateOnly today) => new()
    {
        Id = search.Id,
        UserId = search.UserId,
        Name = search.Name,
        Filters = new SavedSearchFiltersDto
        {
            CampusId = search.CampusId,
            MinRent = search.MinRent,
            MaxRent = search.MaxRent,
            MinArea = search.MinArea,
            MaxArea = search.MaxArea,
            RoomType = search.RoomType,
            MaxDistanceMeters = search.MaxDistanceMeters
        },
        Polygon = search.Polygon,
        AlertsEnabled = search.AlertsEnabled,
        AlertsSentToday = search.AlertsSentOn(today),
        MaxAlertsPerDay = SavedSearchAlertRules.MaxAlertsPerDay,
        LastAlertAt = search.LastAlertAt,
        CreatedAt = search.CreatedAt,
        UpdatedAt = search.UpdatedAt
    };

    /// <summary>Số alert đã gửi trong <paramref name="today"/> (0 nếu bộ đếm thuộc ngày cũ).</summary>
    public static int AlertsSentOn(this SavedSearchDocument search, DateOnly today) =>
        search.AlertCounterDate == today ? search.AlertsSentToday : 0;

    /// <summary>Ghi dữ liệu request (đã validate) vào document.</summary>
    public static void ApplyRequest(
        this SavedSearchDocument search,
        SaveSearchRequestDto request,
        IReadOnlyList<double[]>? polygon,
        DateTimeOffset now)
    {
        var filters = request.Filters ?? new SavedSearchFiltersDto();
        search.Name = string.IsNullOrWhiteSpace(request.Name)
            ? SavedSearchService.DefaultName(polygon is not null, now)
            : request.Name.Trim();
        search.CampusId = CampusIds.OrDefault(filters.CampusId);
        search.MinRent = filters.MinRent;
        search.MaxRent = filters.MaxRent;
        search.MinArea = filters.MinArea;
        search.MaxArea = filters.MaxArea;
        search.RoomType = string.IsNullOrWhiteSpace(filters.RoomType) ? null : filters.RoomType.Trim();
        search.MaxDistanceMeters = filters.MaxDistanceMeters;
        search.Polygon = polygon?.Select(p => new[] { p[0], p[1] }).ToList();
        search.AlertsEnabled = request.AlertsEnabled;
        search.UpdatedAt = now;
    }
}
