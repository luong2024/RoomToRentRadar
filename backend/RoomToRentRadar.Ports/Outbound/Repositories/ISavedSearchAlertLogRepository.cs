using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface ISavedSearchAlertLogRepository
{
    Task<HashSet<(Guid SavedSearchId, Guid ListingId)>> GetAlertedPairsAsync(
        IReadOnlyCollection<Guid> savedSearchIds,
        IReadOnlyCollection<Guid> listingIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, int>> GetDeliveredCountsAsync(
        DateTime dayStartUtc,
        DateTime dayEndUtc,
        IReadOnlyCollection<Guid> savedSearchIds,
        CancellationToken cancellationToken);

    void Add(SavedSearchAlertLog log);
}
