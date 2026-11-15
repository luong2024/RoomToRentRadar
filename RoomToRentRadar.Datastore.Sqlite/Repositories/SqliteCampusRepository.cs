using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Datastore.Sqlite.Repositories;

/// <summary>
/// Read-only access to the seeded <c>Campuses</c> reference table (ERD §4.1, BR-CMP-01..03).
/// Rows come from <see cref="Seed.CampusSeedData"/> and are never written at runtime.
/// </summary>
public sealed class SqliteCampusRepository : ICampusRepository
{
    private const string SelectColumns = """
        Id, Name, ShortName, Latitude, Longitude, IsDefault, SortOrder
        """;

    private readonly SqliteDbContext _dbContext;

    public SqliteCampusRepository(SqliteDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<Campus?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT {SelectColumns} FROM Campuses WHERE Id = @id LIMIT 1;";
            cmd.Parameters.AddWithValue("@id", id);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            return await reader.ReadAsync(ct) ? MapCampus(reader) : null;
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    public async Task<Campus?> GetDefaultCampusAsync(CancellationToken ct = default)
    {
        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT {SelectColumns} FROM Campuses WHERE IsDefault = 1 ORDER BY SortOrder, Id LIMIT 1;";

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            return await reader.ReadAsync(ct) ? MapCampus(reader) : null;
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    public async Task<IReadOnlyList<Campus>> GetAllAsync(CancellationToken ct = default)
    {
        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT {SelectColumns} FROM Campuses ORDER BY SortOrder, Id;";

            var campuses = new List<Campus>(8);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                campuses.Add(MapCampus(reader));
            }

            return campuses;
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    private static Campus MapCampus(SqliteDataReader reader) => new(
        id: reader.GetString(0),
        name: reader.GetString(1),
        shortName: reader.GetString(2),
        latitude: reader.GetDouble(3),
        longitude: reader.GetDouble(4),
        isDefault: reader.GetInt64(5) != 0,
        sortOrder: reader.GetInt32(6));

    private void ReleaseConnection(SqliteConnection conn)
    {
        if (!_dbContext.OwnsSharedConnection)
        {
            conn.Dispose();
        }
    }
}
