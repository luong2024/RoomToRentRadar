using System.Collections.Concurrent;
using RoomToRentRadar.Api.Models;

namespace RoomToRentRadar.Api.Repositories;

public sealed class InMemoryModerationAuditRepository : IModerationAuditRepository
{
    private readonly ConcurrentDictionary<string, List<ModerationAuditDocument>> _byListing =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<ModerationAuditDocument> AddAsync(
        ModerationAuditDocument entry,
        CancellationToken cancellationToken = default)
    {
        var list = _byListing.GetOrAdd(entry.ListingId, static _ => []);
        lock (list)
        {
            list.Add(entry);
        }

        return Task.FromResult(entry);
    }

    public Task<IReadOnlyList<ModerationAuditDocument>> GetByListingIdAsync(
        string listingId,
        CancellationToken cancellationToken = default)
    {
        if (!_byListing.TryGetValue(listingId, out var list))
        {
            return Task.FromResult<IReadOnlyList<ModerationAuditDocument>>([]);
        }

        lock (list)
        {
            IReadOnlyList<ModerationAuditDocument> ordered = list
                .OrderByDescending(x => x.CreatedAt)
                .ToList();
            return Task.FromResult(ordered);
        }
    }

    public Task<IReadOnlyDictionary<string, ModerationAuditDocument>> GetLatestByListingIdsAsync(
        IEnumerable<string> listingIds,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, ModerationAuditDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var listingId in listingIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!_byListing.TryGetValue(listingId, out var list))
            {
                continue;
            }

            lock (list)
            {
                var latest = list.OrderByDescending(x => x.CreatedAt).FirstOrDefault();
                if (latest is not null)
                {
                    result[listingId] = latest;
                }
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, ModerationAuditDocument>>(result);
    }
}
