using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Datastore.Sqlite.Repositories;

/// <summary>
/// SQLite persistence for <c>TravelTimeEstimates</c> (ERD §4.15, BR-TRV-03).
/// Phase 0 stub; full SQL queries added in Phase 2.
/// </summary>
public sealed class SqliteTravelTimeCacheRepository : ITravelTimeCacheRepository
{
    private readonly SqliteDbContext _dbContext;

    public SqliteTravelTimeCacheRepository(SqliteDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<IReadOnlyList<TravelTimeCacheEntry>> GetByRoomAndCampusAsync(
        string roomId,
        string campusId,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TravelTimeCacheEntry>>(Array.Empty<TravelTimeCacheEntry>());

    public Task UpsertRangeAsync(IReadOnlyList<TravelTimeCacheEntry> entries, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task InvalidateByRoomIdAsync(string roomId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<int> PruneExpiredAsync(DateTime expiresBeforeUtc, CancellationToken ct = default) =>
        Task.FromResult(0);
}
