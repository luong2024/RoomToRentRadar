using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Shared.SavedSearches;

namespace RoomToRentRadar.SqliteDataAccess.Repositories;

public sealed class SavedSearchRepository(AppDbContext dbContext) : ISavedSearchRepository
{
    public async Task AddAsync(SavedSearch savedSearch, CancellationToken cancellationToken)
        => await dbContext.SavedSearches.AddAsync(savedSearch, cancellationToken);

    public Task<SavedSearch?> FindAsync(
        Guid userId,
        Guid savedSearchId,
        CancellationToken cancellationToken)
        => dbContext.SavedSearches.FirstOrDefaultAsync(
            savedSearch => savedSearch.Id == savedSearchId && savedSearch.UserId == userId,
            cancellationToken);

    public void Remove(SavedSearch savedSearch) => dbContext.SavedSearches.Remove(savedSearch);

    public async Task<IReadOnlyList<SavedSearch>> GetActiveAsync(CancellationToken cancellationToken)
        => await dbContext.SavedSearches
            .AsNoTracking()
            .Where(savedSearch => savedSearch.AlertState == SavedSearchAlertState.Active)
            .ToListAsync(cancellationToken);
}
