using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Datastore.Sqlite.Repositories;

/// <summary>
/// CQRS read model of US04 search (plan §3.4C, M2, ADR-006): one parameterised SELECT that pushes
/// <c>Status = 'Available'</c>, rent/area/room-type filters and the bounding-box prefilter down to
/// the ERD indexes. Exact Haversine distance and sorting stay in the Application layer.
/// </summary>
public sealed class SqliteRoomSearchReadModel : IRoomSearchReadModel
{
    private readonly SqliteDbContext _dbContext;

    public SqliteRoomSearchReadModel(SqliteDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<RoomSearchCandidate>> QueryCandidatesAsync(
        RoomSearchQueryFilter filter,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var sql = new StringBuilder("""
            SELECT Id, Title, MonthlyRentVnd, AreaM2, RoomType, Status,
                   (VerifiedAtUtc IS NOT NULL) AS IsVerified,
                   NULL AS CoverThumbUrl,
                   Latitude, Longitude, PublishedAtUtc
            FROM Rooms
            WHERE Status = 'Available'
              AND (@MinRent IS NULL OR MonthlyRentVnd >= @MinRent)
              AND (@MaxRent IS NULL OR MonthlyRentVnd <= @MaxRent)
              AND (@MinArea IS NULL OR AreaM2 >= @MinArea)
              AND (@MaxArea IS NULL OR AreaM2 <= @MaxArea)
              AND (@MinLat IS NULL OR Latitude >= @MinLat)
              AND (@MaxLat IS NULL OR Latitude <= @MaxLat)
              AND (@MinLng IS NULL OR Longitude >= @MinLng)
              AND (@MaxLng IS NULL OR Longitude <= @MaxLng)
            """);

        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.Parameters.AddWithValue("@MinRent", (object?)filter.MinRentVnd ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MaxRent", (object?)filter.MaxRentVnd ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MinArea", (object?)filter.MinAreaM2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MaxArea", (object?)filter.MaxAreaM2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MinLat", (object?)filter.MinLat ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MaxLat", (object?)filter.MaxLat ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MinLng", (object?)filter.MinLng ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MaxLng", (object?)filter.MaxLng ?? DBNull.Value);

            if (filter.RoomTypes is { Count: > 0 } roomTypes)
            {
                // Parameterised IN list — never interpolate the enum values into SQL.
                var placeholders = new string[roomTypes.Count];
                for (var i = 0; i < roomTypes.Count; i++)
                {
                    var name = $"@RoomType{i}";
                    placeholders[i] = name;
                    cmd.Parameters.AddWithValue(name, roomTypes[i].ToString());
                }

                sql.Append(CultureInfo.InvariantCulture, $"\n  AND RoomType IN ({string.Join(", ", placeholders)})");
            }

            sql.Append("\nORDER BY PublishedAtUtc DESC, Id ASC;");
            cmd.CommandText = sql.ToString();

            var candidates = new List<RoomSearchCandidate>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                candidates.Add(new RoomSearchCandidate(
                    Id: reader.GetString(0),
                    Title: reader.GetString(1),
                    MonthlyRentVnd: reader.GetInt64(2),
                    AreaM2: reader.GetDouble(3),
                    RoomType: Enum.Parse<RoomType>(reader.GetString(4), ignoreCase: false),
                    Status: Enum.Parse<ListingStatus>(reader.GetString(5), ignoreCase: false),
                    IsVerified: reader.GetInt64(6) != 0,
                    CoverThumbUrl: reader.IsDBNull(7) ? null : reader.GetString(7),
                    Latitude: reader.GetDouble(8),
                    Longitude: reader.GetDouble(9),
                    PublishedAtUtc: reader.IsDBNull(10)
                        ? null
                        : DateTime.Parse(reader.GetString(10), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
            }

            return candidates;
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    private void ReleaseConnection(SqliteConnection conn)
    {
        if (!_dbContext.OwnsSharedConnection)
        {
            conn.Dispose();
        }
    }
}
