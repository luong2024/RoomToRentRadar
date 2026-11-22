using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface ISavedSearchRepository
{
    Task AddAsync(SavedSearch savedSearch, CancellationToken cancellationToken);

    Task<SavedSearch?> FindAsync(
        Guid userId,
        Guid savedSearchId,
        CancellationToken cancellationToken);

    void Remove(SavedSearch savedSearch);

    Task<IReadOnlyList<SavedSearch>> GetActiveAsync(CancellationToken cancellationToken);
}
