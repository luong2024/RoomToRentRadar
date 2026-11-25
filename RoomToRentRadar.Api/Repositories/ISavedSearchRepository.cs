using RoomToRentRadar.Api.Models;

namespace RoomToRentRadar.Api.Repositories;

/// <summary>Persistence cho saved search (US07 / T07.1). Bản Cosmos dùng container UsersInteractions, PK /userId.</summary>
public interface ISavedSearchRepository
{
    Task<IReadOnlyList<SavedSearchDocument>> GetByUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<SavedSearchDocument?> GetAsync(string userId, string id, CancellationToken cancellationToken = default);

    /// <summary>Tất cả saved search đang bật alert (cross-partition; worker T07.2 dùng khi có listing mới được duyệt).</summary>
    Task<IReadOnlyList<SavedSearchDocument>> GetAlertEnabledAsync(CancellationToken cancellationToken = default);

    Task<int> CountByUserAsync(string userId, CancellationToken cancellationToken = default);

    Task UpsertAsync(SavedSearchDocument document, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string userId, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tăng bộ đếm alert một cách nguyên tử nếu chưa chạm <paramref name="maxPerDay"/> trong ngày <paramref name="today"/>
    /// và listing chưa từng được alert. Cosmos: Patch với điều kiện ETag.
    /// </summary>
    Task<SavedSearchDocument?> TryRecordAlertAsync(
        string userId,
        string id,
        string listingId,
        DateOnly today,
        int maxPerDay,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken = default);
}
