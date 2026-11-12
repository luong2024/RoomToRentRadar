using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Datastore.Sqlite.Repositories;

/// <summary>
/// Phase 0 stub: only the new <see cref="IRoomRepository"/> shape exists here.
/// Task 3 rewrites this against the ERD §4.4 schema (Campuses + Rooms, optimistic concurrency).
/// </summary>
public sealed class SqliteRoomRepository : IRoomRepository
{
    private readonly SqliteDbContext _dbContext;

    public SqliteRoomRepository(SqliteDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<Room?> GetByIdAsync(string id, CancellationToken ct = default) =>
        throw new NotImplementedException("SqliteRoomRepository.GetByIdAsync is implemented in Task 3 (Giai đoạn 3).");

    public Task AddAsync(Room room, CancellationToken ct = default) =>
        throw new NotImplementedException("SqliteRoomRepository.AddAsync is implemented in Task 3 (Giai đoạn 3).");

    public Task UpdateAsync(Room room, CancellationToken ct = default) =>
        throw new NotImplementedException("SqliteRoomRepository.UpdateAsync is implemented in Task 3 (Giai đoạn 3).");
}
