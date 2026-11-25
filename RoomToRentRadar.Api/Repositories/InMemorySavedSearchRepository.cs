using RoomToRentRadar.Api.Models;

namespace RoomToRentRadar.Api.Repositories;

/// <summary>Bản in-memory dùng cho dev/test khi chưa bật Cosmos DB Emulator.</summary>
public sealed class InMemorySavedSearchRepository : ISavedSearchRepository
{
    private readonly Dictionary<string, SavedSearchDocument> _items = new(StringComparer.Ordinal);
    private readonly Lock _sync = new();

    public Task<IReadOnlyList<SavedSearchDocument>> GetByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            IReadOnlyList<SavedSearchDocument> items = _items.Values
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.UpdatedAt)
                .Select(Clone)
                .ToList();
            return Task.FromResult(items);
        }
    }

    public Task<SavedSearchDocument?> GetAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            return Task.FromResult(Find(userId, id) is { } doc ? Clone(doc) : null);
        }
    }

    public Task<IReadOnlyList<SavedSearchDocument>> GetAlertEnabledAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            IReadOnlyList<SavedSearchDocument> items = _items.Values.Where(s => s.AlertsEnabled).Select(Clone).ToList();
            return Task.FromResult(items);
        }
    }

    public Task<int> CountByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            return Task.FromResult(_items.Values.Count(s => s.UserId == userId));
        }
    }

    public Task UpsertAsync(SavedSearchDocument document, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _items[Key(document.UserId, document.Id)] = Clone(document);
        }

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            return Task.FromResult(_items.Remove(Key(userId, id)));
        }
    }

    public Task<SavedSearchDocument?> TryRecordAlertAsync(
        string userId,
        string id,
        string listingId,
        DateOnly today,
        int maxPerDay,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var doc = Find(userId, id);
            if (doc is null || !doc.AlertsEnabled || doc.AlertedListingIds.Contains(listingId))
            {
                return Task.FromResult<SavedSearchDocument?>(null);
            }

            var sentToday = doc.AlertCounterDate == today ? doc.AlertsSentToday : 0;
            if (sentToday >= maxPerDay)
            {
                return Task.FromResult<SavedSearchDocument?>(null);
            }

            doc.AlertCounterDate = today;
            doc.AlertsSentToday = sentToday + 1;
            doc.LastAlertAt = sentAt;
            doc.AlertedListingIds.Add(listingId);
            return Task.FromResult<SavedSearchDocument?>(Clone(doc));
        }
    }

    private SavedSearchDocument? Find(string userId, string id) =>
        _items.TryGetValue(Key(userId, id), out var doc) ? doc : null;

    private static string Key(string userId, string id) => $"{userId}|{id}";

    // Trả bản sao để caller không sửa trực tiếp state (giống hành vi đọc từ Cosmos).
    private static SavedSearchDocument Clone(SavedSearchDocument s) => new()
    {
        Id = s.Id,
        UserId = s.UserId,
        Type = s.Type,
        Name = s.Name,
        CampusId = s.CampusId,
        MinRent = s.MinRent,
        MaxRent = s.MaxRent,
        MinArea = s.MinArea,
        MaxArea = s.MaxArea,
        RoomType = s.RoomType,
        MaxDistanceMeters = s.MaxDistanceMeters,
        Polygon = s.Polygon?.Select(p => (double[])p.Clone()).ToList(),
        AlertsEnabled = s.AlertsEnabled,
        AlertsSentToday = s.AlertsSentToday,
        AlertCounterDate = s.AlertCounterDate,
        LastAlertAt = s.LastAlertAt,
        AlertedListingIds = [.. s.AlertedListingIds],
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt
    };
}
