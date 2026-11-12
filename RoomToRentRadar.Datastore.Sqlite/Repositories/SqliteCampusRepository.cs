using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Datastore.Sqlite.Repositories;

/// <summary>
/// Phase 0 stub. Task 3 (Giai đoạn 3) implements this against the <c>Campuses</c> table and the
/// <c>CampusSeedData</c> fixture (BR-CMP-01..03); the constructor shape is final so the tests
/// written in Task 1 compile and stay valid.
/// </summary>
public sealed class SqliteCampusRepository : ICampusRepository
{
    // Kept for Task 3 to reuse; suppressed so the Phase 0 stub does not warn.
#pragma warning disable CS0169
    private readonly SqliteDbContext _dbContext;
#pragma warning restore CS0169

    public SqliteCampusRepository(SqliteDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<Campus?> GetByIdAsync(string id, CancellationToken ct = default) =>
        throw new NotImplementedException("SqliteCampusRepository.GetByIdAsync is implemented in Task 3 (Giai đoạn 3).");

    public Task<Campus?> GetDefaultCampusAsync(CancellationToken ct = default) =>
        throw new NotImplementedException("SqliteCampusRepository.GetDefaultCampusAsync is implemented in Task 3 (Giai đoạn 3).");

    public Task<IReadOnlyList<Campus>> GetAllAsync(CancellationToken ct = default) =>
        throw new NotImplementedException("SqliteCampusRepository.GetAllAsync is implemented in Task 3 (Giai đoạn 3).");
}
