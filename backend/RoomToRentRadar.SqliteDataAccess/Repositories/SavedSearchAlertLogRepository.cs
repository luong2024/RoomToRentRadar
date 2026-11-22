using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.SqliteDataAccess.Repositories;

public sealed class SavedSearchAlertLogRepository(AppDbContext dbContext)
    : ISavedSearchAlertLogRepository
{
    public async Task<HashSet<(Guid SavedSearchId, Guid ListingId)>> GetAlertedPairsAsync(
        IReadOnlyCollection<Guid> savedSearchIds,
        IReadOnlyCollection<Guid> listingIds,
        CancellationToken cancellationToken)
        => (await dbContext.SavedSearchAlertLogs
                .AsNoTracking()
                .Where(log => savedSearchIds.Contains(log.SavedSearchId)
                    && listingIds.Contains(log.ListingId))
                .Select(log => new { log.SavedSearchId, log.ListingId })
                .ToListAsync(cancellationToken))
            .Select(log => (log.SavedSearchId, log.ListingId))
            .ToHashSet();

    public async Task<IReadOnlyDictionary<Guid, int>> GetDeliveredCountsAsync(
        DateTime dayStartUtc,
        DateTime dayEndUtc,
        IReadOnlyCollection<Guid> savedSearchIds,
        CancellationToken cancellationToken)
        => (await dbContext.SavedSearchAlertLogs
                .AsNoTracking()
                .Where(log => log.Delivered
                    && log.CreatedAt >= dayStartUtc
                    && log.CreatedAt < dayEndUtc
                    && savedSearchIds.Contains(log.SavedSearchId))
                .GroupBy(log => log.SavedSearchId)
                .Select(group => new { SavedSearchId = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(row => row.SavedSearchId, row => row.Count);

    public void Add(SavedSearchAlertLog log) => dbContext.SavedSearchAlertLogs.Add(log);
}
