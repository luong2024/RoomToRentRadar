using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Datastore.Sqlite.Repositories;

/// <summary>
/// CQRS read model of US04 search (plan §3.4C, M2, ADR-006): one parameterised SELECT that pushes
/// <c>Status = 'Available'</c>, the BR-LST-06 landlord-verified rule (<c>Users.IsVerified</c>),
/// rent/area/room-type filters and the bounding-box prefilter down to SQL. Exact Haversine distance
/// and ordering stay in the Application layer, so the query deliberately has no ORDER BY.
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

        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var cmd = CreateCandidateQueryCommand(conn, filter);

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
                    CoverThumbPath: reader.IsDBNull(7) ? null : reader.GetString(7),
                    Latitude: reader.GetDouble(8),
                    Longitude: reader.GetDouble(9),
                    PublishedAtUtc: reader.IsDBNull(10)
                        ? null
                        : DateTime.Parse(reader.GetString(10), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    Furnishing: reader.IsDBNull(11) ? null : ParseFurnishing(reader.GetString(11))));
            }

            return candidates;
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    /// <summary>
    /// Builds the parameterised candidate query command (shared with <c>EXPLAIN QUERY PLAN</c> tests so
    /// query-plan assertions always run against the exact SQL produced at runtime).
    /// </summary>
    internal static SqliteCommand CreateCandidateQueryCommand(SqliteConnection conn, RoomSearchQueryFilter filter)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentNullException.ThrowIfNull(filter);

        var sql = new StringBuilder("""
            SELECT r.Id, r.Title, r.MonthlyRentVnd, r.AreaM2, r.RoomType, r.Status,
                   (r.VerifiedAtUtc IS NOT NULL) AS IsVerified,
                   (SELECT p.ThumbPath FROM RoomPhotos p
                    WHERE p.RoomId = r.Id AND p.Kind = 'Standard'
                    ORDER BY p.SortOrder, p.Id LIMIT 1) AS CoverThumbPath,
                   r.Latitude, r.Longitude, r.PublishedAtUtc, r.Furnishing
            FROM Rooms r
            INNER JOIN Users u ON u.Id = r.LandlordId
            WHERE r.Status = 'Available'
              AND u.IsVerified = 1
              AND r.VerifiedAtUtc IS NOT NULL
            """);

        // BR-LST-06 / T04.3: only append conditions that are actually requested. The previous
        // "(@Param IS NULL OR r.Column >= @Param)" shape prevented SQLite from pushing the range into
        // IX_Rooms_Status_Rent / IX_Rooms_Status_Lat_Lng (full table scan on Rooms).
        var cmd = conn.CreateCommand();
        try
        {
            if (filter.MinRentVnd is { } minRentVnd)
            {
                sql.Append("\n  AND r.MonthlyRentVnd >= @MinRent");
                cmd.Parameters.AddWithValue("@MinRent", minRentVnd);
            }

            if (filter.MaxRentVnd is { } maxRentVnd)
            {
                sql.Append("\n  AND r.MonthlyRentVnd <= @MaxRent");
                cmd.Parameters.AddWithValue("@MaxRent", maxRentVnd);
            }

            if (filter.MinAreaM2 is { } minAreaM2)
            {
                sql.Append("\n  AND r.AreaM2 >= @MinArea");
                cmd.Parameters.AddWithValue("@MinArea", minAreaM2);
            }

            if (filter.MaxAreaM2 is { } maxAreaM2)
            {
                sql.Append("\n  AND r.AreaM2 <= @MaxArea");
                cmd.Parameters.AddWithValue("@MaxArea", maxAreaM2);
            }

            if (filter.MinLat is { } minLat)
            {
                sql.Append("\n  AND r.Latitude >= @MinLat");
                cmd.Parameters.AddWithValue("@MinLat", minLat);
            }

            if (filter.MaxLat is { } maxLat)
            {
                sql.Append("\n  AND r.Latitude <= @MaxLat");
                cmd.Parameters.AddWithValue("@MaxLat", maxLat);
            }

            if (filter.MinLng is { } minLng)
            {
                sql.Append("\n  AND r.Longitude >= @MinLng");
                cmd.Parameters.AddWithValue("@MinLng", minLng);
            }

            if (filter.MaxLng is { } maxLng)
            {
                sql.Append("\n  AND r.Longitude <= @MaxLng");
                cmd.Parameters.AddWithValue("@MaxLng", maxLng);
            }

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

                sql.Append(CultureInfo.InvariantCulture, $"\n  AND r.RoomType IN ({string.Join(", ", placeholders)})");
            }

            cmd.CommandText = sql.ToString();
            return cmd;
        }
        catch
        {
            cmd.Dispose();
            throw;
        }
    }

    // Furnishing is stored as the enum name; an unknown legacy value is reported as "not stated".
    private static Furnishing? ParseFurnishing(string value) =>
        Enum.TryParse<Furnishing>(value, ignoreCase: true, out var furnishing) && Enum.IsDefined(furnishing)
            ? furnishing
            : null;

    private void ReleaseConnection(SqliteConnection conn)
    {
        if (!_dbContext.OwnsSharedConnection)
        {
            conn.Dispose();
        }
    }
}
