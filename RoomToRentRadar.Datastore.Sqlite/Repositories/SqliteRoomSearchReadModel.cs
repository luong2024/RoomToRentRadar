using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Datastore.Sqlite.Repositories;

/// <summary>
/// Phase 0 stub. Task 3 (Giai đoạn 3) implements the parameterised SQL of plan §3.4C
/// (<c>Status = 'Available'</c> + rent/area/room-type/bbox filters on the ERD indexes).
/// </summary>
public sealed class SqliteRoomSearchReadModel : IRoomSearchReadModel
{
    // Kept for Task 3 to reuse; suppressed so the Phase 0 stub does not warn.
#pragma warning disable CS0169
    private readonly SqliteDbContext _dbContext;
#pragma warning restore CS0169

    public SqliteRoomSearchReadModel(SqliteDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<IReadOnlyList<RoomSearchCandidate>> QueryCandidatesAsync(
        RoomSearchQueryFilter filter,
        CancellationToken ct = default) =>
        throw new NotImplementedException("SqliteRoomSearchReadModel.QueryCandidatesAsync is implemented in Task 3 (Giai đoạn 3).");
}
