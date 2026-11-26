using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services.Mapping;
using RoomToRentRadar.Api.Services.Search;
using RoomToRentRadar.Api.Services.Time;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services.SavedSearches;

public interface ISavedSearchService
{
    Task<IReadOnlyList<SavedSearchDto>> GetMineAsync(string userId, CancellationToken cancellationToken = default);

    Task<SavedSearchDto?> GetAsync(string userId, string id, CancellationToken cancellationToken = default);

    /// <exception cref="RequestValidationException">Request không hợp lệ.</exception>
    Task<SavedSearchDto> CreateAsync(string userId, SaveSearchRequestDto request, CancellationToken cancellationToken = default);

    /// <returns>null nếu không tìm thấy.</returns>
    /// <exception cref="RequestValidationException">Request không hợp lệ.</exception>
    Task<SavedSearchDto?> UpdateAsync(string userId, string id, SaveSearchRequestDto request, CancellationToken cancellationToken = default);

    Task<SavedSearchDto?> SetAlertsAsync(string userId, string id, bool alertsEnabled, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string userId, string id, CancellationToken cancellationToken = default);

    /// <summary>Chạy lại saved search trên dữ liệu hiện tại (để MAUI mở nhanh kết quả).</summary>
    Task<SavedSearchMatchesDto?> GetMatchesAsync(string userId, string id, CancellationToken cancellationToken = default);
}

/// <summary>
/// US07 / T07.1 — CRUD saved search. Validate ở <see cref="SavedSearchRequestValidator"/>,
/// map ở <see cref="SavedSearchMapper"/>, rule khớp ở <see cref="SavedSearchMatcher"/>.
/// </summary>
public sealed class SavedSearchService(
    ISavedSearchRepository repository,
    IListingRepository listingRepository,
    TimeProvider timeProvider) : ISavedSearchService
{
    public const int MaxSavedSearchesPerUser = 20;
    public const int MaxNameLength = SavedSearchRequestValidator.MaxNameLength;

    public async Task<IReadOnlyList<SavedSearchDto>> GetMineAsync(string userId, CancellationToken cancellationToken = default)
    {
        var items = await repository.GetByUserAsync(userId, cancellationToken);
        var today = timeProvider.GetVietnamToday();
        return items.Select(s => SavedSearchMapper.ToDto(s, today)).ToList();
    }

    public async Task<SavedSearchDto?> GetAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        var doc = await repository.GetAsync(userId, id, cancellationToken);
        return doc is null ? null : ToDto(doc);
    }

    public async Task<SavedSearchDto> CreateAsync(string userId, SaveSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        var polygon = SavedSearchRequestValidator.Validate(request);
        await EnsureQuotaAsync(userId, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var doc = new SavedSearchDocument { UserId = userId, CreatedAt = now };
        doc.ApplyRequest(request, polygon, now);

        await repository.UpsertAsync(doc, cancellationToken);
        return ToDto(doc);
    }

    public async Task<SavedSearchDto?> UpdateAsync(string userId, string id, SaveSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        var polygon = SavedSearchRequestValidator.Validate(request);
        return await ModifyAsync(userId, id, (doc, now) => doc.ApplyRequest(request, polygon, now), cancellationToken);
    }

    public Task<SavedSearchDto?> SetAlertsAsync(string userId, string id, bool alertsEnabled, CancellationToken cancellationToken = default) =>
        ModifyAsync(userId, id, (doc, now) =>
        {
            doc.AlertsEnabled = alertsEnabled;
            doc.UpdatedAt = now;
        }, cancellationToken);

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
        var matched = listings.Where(l => SavedSearchMatcher.Matches(doc, l));
        var items = ListingSorter.Apply(matched, ListingSortKeys.Distance).Select(ListingPinMapper.ToPin).ToList();

        return new SavedSearchMatchesDto { SavedSearchId = doc.Id, TotalCount = items.Count, Items = items };
    }

    /// <summary>Tên mặc định khi UI không gửi tên, ví dụ "Khu vực đã vẽ 25/11 09:30" (giờ VN).</summary>
    public static string DefaultName(bool hasPolygon, DateTimeOffset now) =>
        $"{(hasPolygon ? "Khu vực đã vẽ" : "Tìm kiếm đã lưu")} {now.ToVietnamTime():dd/MM HH:mm}";

    private async Task EnsureQuotaAsync(string userId, CancellationToken cancellationToken)
    {
        if (await repository.CountByUserAsync(userId, cancellationToken) >= MaxSavedSearchesPerUser)
        {
            throw RequestValidationException.For("name", $"Mỗi tài khoản lưu tối đa {MaxSavedSearchesPerUser} tìm kiếm.");
        }
    }

    /// <summary>Đọc → sửa → lưu; null nếu không tìm thấy.</summary>
    private async Task<SavedSearchDto?> ModifyAsync(
        string userId,
        string id,
        Action<SavedSearchDocument, DateTimeOffset> change,
        CancellationToken cancellationToken)
    {
        var doc = await repository.GetAsync(userId, id, cancellationToken);
        if (doc is null)
        {
            return null;
        }

        change(doc, timeProvider.GetUtcNow());
        await repository.UpsertAsync(doc, cancellationToken);
        return ToDto(doc);
    }

    private SavedSearchDto ToDto(SavedSearchDocument doc) => SavedSearchMapper.ToDto(doc, timeProvider.GetVietnamToday());
}
