using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services.SavedSearches;

/// <summary>
/// Hợp đồng bàn giao cho push worker T07.2 (Nguyễn Minh Hiếu).
/// Worker gọi <see cref="GetAlertCandidatesAsync"/> khi listing chuyển sang Verified,
/// sau đó với mỗi candidate gọi <see cref="TryRecordAlertAsync"/>; chỉ gửi push khi kết quả = true.
/// </summary>
public interface ISavedSearchAlertRules
{
    /// <summary>Các saved search (đang bật alert, chưa chạm 3 alert/ngày) khớp với listing vừa được duyệt.</summary>
    Task<IReadOnlyList<SavedSearchAlertDto>> GetAlertCandidatesAsync(string listingId, CancellationToken cancellationToken = default);

    /// <summary>Giữ chỗ 1 alert (tăng bộ đếm nguyên tử). false = đã đủ 3 alert hôm nay / đã alert listing này / alert bị tắt.</summary>
    Task<bool> TryRecordAlertAsync(SavedSearchAlertDto alert, CancellationToken cancellationToken = default);
}

/// <summary>US07 / T07.1 — alert rules: khớp bộ lọc + polygon, tối đa 3 alert/ngày/saved search (US07 AC4), không gửi trùng listing.</summary>
public sealed class SavedSearchAlertRules(
    ISavedSearchRepository repository,
    IListingRepository listingRepository,
    TimeProvider timeProvider) : ISavedSearchAlertRules
{
    public const int MaxAlertsPerDay = 3;

    /// <summary>Bộ đếm reset lúc 00:00 giờ Việt Nam (UTC+7, không có DST).</summary>
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static DateOnly TodayInVietnam(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetUtcNow().ToOffset(VietnamOffset).DateTime);

    public async Task<IReadOnlyList<SavedSearchAlertDto>> GetAlertCandidatesAsync(
        string listingId,
        CancellationToken cancellationToken = default)
    {
        var listing = await listingRepository.GetByIdAsync(listingId, cancellationToken);
        if (listing is null || !SavedSearchMatcher.IsPublic(listing))
        {
            return [];
        }

        var today = TodayInVietnam(timeProvider);
        var searches = await repository.GetAlertEnabledAsync(cancellationToken);

        return searches
            .Where(s => !s.AlertedListingIds.Contains(listing.Id))
            .Select(s => (Search: s, SentToday: s.AlertCounterDate == today ? s.AlertsSentToday : 0))
            .Where(x => x.SentToday < MaxAlertsPerDay && SavedSearchMatcher.Matches(x.Search, listing))
            .Select(x => new SavedSearchAlertDto
            {
                SavedSearchId = x.Search.Id,
                SavedSearchName = x.Search.Name,
                UserId = x.Search.UserId,
                ListingId = listing.Id,
                ListingTitle = listing.Title,
                Rent = listing.Rent,
                RemainingAlertsToday = MaxAlertsPerDay - x.SentToday - 1
            })
            .ToList();
    }

    public async Task<bool> TryRecordAlertAsync(SavedSearchAlertDto alert, CancellationToken cancellationToken = default)
    {
        var updated = await repository.TryRecordAlertAsync(
            alert.UserId,
            alert.SavedSearchId,
            alert.ListingId,
            TodayInVietnam(timeProvider),
            MaxAlertsPerDay,
            timeProvider.GetUtcNow(),
            cancellationToken);

        return updated is not null;
    }
}
