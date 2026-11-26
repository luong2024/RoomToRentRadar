using RoomToRentRadar.Api.Services.Geo;
using RoomToRentRadar.Api.Services.Mapping;
using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services.SavedSearches;

public interface ISavedSearchService
{
    Task<IReadOnlyList<SavedSearchDto>> GetMineAsync(string userId, CancellationToken cancellationToken = default);

    Task<SavedSearchDto?> GetAsync(string userId, string id, CancellationToken cancellationToken = default);

    /// <exception cref="RequestValidationException">Request không hợp lệ.</exception>
    Task<SavedSearchDto> CreateAsync(string userId, SaveSearchRequestDto request, CancellationToken cancellationToken = default);

    /// <returns>null nếu không tìm thấy.</returns>
    Task<SavedSearchDto?> UpdateAsync(string userId, string id, SaveSearchRequestDto request, CancellationToken cancellationToken = default);

    Task<SavedSearchDto?> SetAlertsAsync(string userId, string id, bool alertsEnabled, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string userId, string id, CancellationToken cancellationToken = default);

    /// <summary>Chạy lại saved search trên dữ liệu hiện tại (để MAUI mở nhanh kết quả).</summary>
    Task<SavedSearchMatchesDto?> GetMatchesAsync(string userId, string id, CancellationToken cancellationToken = default);
}

/// <summary>US07 / T07.1 — CRUD saved search + validate alert rule.</summary>
public sealed class SavedSearchService(
    ISavedSearchRepository repository,
    IListingRepository listingRepository,
    TimeProvider timeProvider) : ISavedSearchService
{
    public const int MaxSavedSearchesPerUser = 20;
    public const int MaxNameLength = 100;

    public async Task<IReadOnlyList<SavedSearchDto>> GetMineAsync(string userId, CancellationToken cancellationToken = default)
    {
        var items = await repository.GetByUserAsync(userId, cancellationToken);
        var today = SavedSearchAlertRules.TodayInVietnam(timeProvider);
        return items.Select(s => ToDto(s, today)).ToList();
    }

    public async Task<SavedSearchDto?> GetAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        var doc = await repository.GetAsync(userId, id, cancellationToken);
        return doc is null ? null : ToDto(doc, SavedSearchAlertRules.TodayInVietnam(timeProvider));
    }

    public async Task<SavedSearchDto> CreateAsync(string userId, SaveSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        var polygon = Validate(request);

        if (await repository.CountByUserAsync(userId, cancellationToken) >= MaxSavedSearchesPerUser)
        {
            throw RequestValidationException.For("name", $"Mỗi tài khoản lưu tối đa {MaxSavedSearchesPerUser} tìm kiếm.");
        }

        var now = timeProvider.GetUtcNow();
        var doc = new SavedSearchDocument { UserId = userId, CreatedAt = now };
        Apply(doc, request, polygon, now);

        await repository.UpsertAsync(doc, cancellationToken);
        return ToDto(doc, SavedSearchAlertRules.TodayInVietnam(timeProvider));
    }

    public async Task<SavedSearchDto?> UpdateAsync(string userId, string id, SaveSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        var polygon = Validate(request);
        var doc = await repository.GetAsync(userId, id, cancellationToken);
        if (doc is null)
        {
            return null;
        }

        Apply(doc, request, polygon, timeProvider.GetUtcNow());
        await repository.UpsertAsync(doc, cancellationToken);
        return ToDto(doc, SavedSearchAlertRules.TodayInVietnam(timeProvider));
    }

    public async Task<SavedSearchDto?> SetAlertsAsync(string userId, string id, bool alertsEnabled, CancellationToken cancellationToken = default)
    {
        var doc = await repository.GetAsync(userId, id, cancellationToken);
        if (doc is null)
        {
            return null;
        }

        doc.AlertsEnabled = alertsEnabled;
        doc.UpdatedAt = timeProvider.GetUtcNow();
        await repository.UpsertAsync(doc, cancellationToken);
        return ToDto(doc, SavedSearchAlertRules.TodayInVietnam(timeProvider));
    }

    public Task<bool> DeleteAsync(string userId, string id, CancellationToken cancellationToken = default) =>
        repository.DeleteAsync(userId, id, cancellationToken);

    public async Task<SavedSearchMatchesDto?> GetMatchesAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        var doc = await repository.GetAsync(userId, id, cancellationToken);
        if (doc is null)
        {
            return null;
        }

        var listings = await listingRepository.GetPublicListingsAsync(doc.CampusId, cancellationToken);
        var items = listings
            .Where(l => SavedSearchMatcher.Matches(doc, l))
            .OrderBy(l => l.DistanceMeters)
            .ThenBy(l => l.Rent)
            .Select(ListingPinMapper.ToPin)
            .ToList();

        return new SavedSearchMatchesDto { SavedSearchId = doc.Id, TotalCount = items.Count, Items = items };
    }

    /// <summary>Validate request; trả về polygon đã chuẩn hóa (đóng ring) hoặc null.</summary>
    public static IReadOnlyList<double[]>? Validate(SaveSearchRequestDto? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            throw RequestValidationException.For("body", "Request body is required.");
        }

        // Tên để trống được chấp nhận: server tự đặt tên (xem DefaultName) để UI không phải bắt buộc nhập.
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length > MaxNameLength)
        {
            errors["name"] = [$"Tên tìm kiếm tối đa {MaxNameLength} ký tự."];
        }

        var f = request.Filters ?? new SavedSearchFiltersDto();
        if (f.MinRent < 0 || f.MaxRent < 0) errors["filters.rent"] = ["Giá thuê không được âm."];
        else if (f.MinRent > f.MaxRent) errors["filters.rent"] = ["MinRent phải nhỏ hơn hoặc bằng MaxRent."];

        if (f.MinArea < 0 || f.MaxArea < 0) errors["filters.area"] = ["Diện tích không được âm."];
        else if (f.MinArea > f.MaxArea) errors["filters.area"] = ["MinArea phải nhỏ hơn hoặc bằng MaxArea."];

        if (f.MaxDistanceMeters <= 0) errors["filters.maxDistanceMeters"] = ["Khoảng cách tối đa phải lớn hơn 0."];

        IReadOnlyList<double[]>? polygon = null;
        if (request.Polygon is { Count: > 0 })
        {
            if (GeoPolygon.TryNormalize(request.Polygon, out var ring, out var polygonError))
            {
                polygon = ring;
            }
            else
            {
                errors[GeoPolygon.FieldName] = [polygonError];
            }
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        return polygon;
    }

    private static void Apply(SavedSearchDocument doc, SaveSearchRequestDto request, IReadOnlyList<double[]>? polygon, DateTimeOffset now)
    {
        var f = request.Filters ?? new SavedSearchFiltersDto();
        doc.Name = string.IsNullOrWhiteSpace(request.Name) ? DefaultName(polygon is not null, now) : request.Name.Trim();
        doc.CampusId = string.IsNullOrWhiteSpace(f.CampusId) ? "ictu-main" : f.CampusId.Trim();
        doc.MinRent = f.MinRent;
        doc.MaxRent = f.MaxRent;
        doc.MinArea = f.MinArea;
        doc.MaxArea = f.MaxArea;
        doc.RoomType = string.IsNullOrWhiteSpace(f.RoomType) ? null : f.RoomType.Trim();
        doc.MaxDistanceMeters = f.MaxDistanceMeters;
        doc.Polygon = polygon?.Select(p => new[] { p[0], p[1] }).ToList();
        doc.AlertsEnabled = request.AlertsEnabled;
        doc.UpdatedAt = now;
    }

    /// <summary>Tên mặc định khi UI không gửi tên, ví dụ "Khu vực đã vẽ 25/11 09:30" (giờ VN).</summary>
    public static string DefaultName(bool hasPolygon, DateTimeOffset now)
    {
        var local = now.ToOffset(SavedSearchAlertRules.VietnamOffset);
        return $"{(hasPolygon ? "Khu vực đã vẽ" : "Tìm kiếm đã lưu")} {local:dd/MM HH:mm}";
    }

    public static SavedSearchDto ToDto(SavedSearchDocument s, DateOnly today) => new()
    {
        Id = s.Id,
        UserId = s.UserId,
        Name = s.Name,
        Filters = new SavedSearchFiltersDto
        {
            CampusId = s.CampusId,
            MinRent = s.MinRent,
            MaxRent = s.MaxRent,
            MinArea = s.MinArea,
            MaxArea = s.MaxArea,
            RoomType = s.RoomType,
            MaxDistanceMeters = s.MaxDistanceMeters
        },
        Polygon = s.Polygon,
        AlertsEnabled = s.AlertsEnabled,
        AlertsSentToday = s.AlertCounterDate == today ? s.AlertsSentToday : 0,
        MaxAlertsPerDay = SavedSearchAlertRules.MaxAlertsPerDay,
        LastAlertAt = s.LastAlertAt,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt
    };
}
