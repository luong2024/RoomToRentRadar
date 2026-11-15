using System.Globalization;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Seed;
using SQLitePCL;

namespace RoomToRentRadar.Datastore.Sqlite.Context;

public sealed class SqliteDbContext : IDisposable
{
    private readonly SqliteSettings _settings;
    private readonly SqliteConnection? _keepAliveInMemoryConnection;
    private bool _schemaInitialized;
    private readonly object _initLock = new();

    static SqliteDbContext()
    {
        Batteries_V2.Init();
    }

    public SqliteDbContext(SqliteSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;

        if (settings.ConnectionString.Contains(":memory:", StringComparison.OrdinalIgnoreCase) ||
            settings.ConnectionString.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase))
        {
            _keepAliveInMemoryConnection = new SqliteConnection(settings.ConnectionString);
            _keepAliveInMemoryConnection.Open();
            EnableForeignKeys(_keepAliveInMemoryConnection);
        }
    }

    public SqliteConnection CreateOpenConnection()
    {
        EnsureSchemaInitialized();

        if (_keepAliveInMemoryConnection != null)
        {
            return _keepAliveInMemoryConnection;
        }

        var connection = new SqliteConnection(_settings.ConnectionString);
        connection.Open();
        EnableForeignKeys(connection);
        return connection;
    }

    public bool OwnsSharedConnection => _keepAliveInMemoryConnection != null;

    public void EnsureSchemaInitialized()
    {
        if (_schemaInitialized)
        {
            return;
        }

        lock (_initLock)
        {
            if (_schemaInitialized)
            {
                return;
            }

            var conn = _keepAliveInMemoryConnection ?? new SqliteConnection(_settings.ConnectionString);
            if (conn != _keepAliveInMemoryConnection)
            {
                conn.Open();
                // PRAGMA foreign_keys is a no-op inside a transaction, so it must precede Migrate.
                EnableForeignKeys(conn);
            }

            try
            {
                SqliteMigrator.Migrate(conn);

                // Campuses is reference data: always seeded, in every environment (H4 / ADR-002).
                SeedCampuses(conn);

                if (_settings.SeedDemoRooms)
                {
                    // BR-LST-06: search only shows rooms whose landlord is verified, so the demo
                    // landlord must exist (and be verified) before the demo rooms are inserted.
                    SeedDemoLandlordUserIfMissing(conn);
                    SeedRoomsIfEmpty(conn);
                }

                _schemaInitialized = true;
            }
            finally
            {
                if (conn != _keepAliveInMemoryConnection)
                {
                    conn.Dispose();
                }
            }
        }
    }

    private static void EnableForeignKeys(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// BR-CMP-01: inserts the eight campuses with <c>INSERT OR IGNORE</c>, so existing rows (and any
    /// manual coordinate correction) are left untouched and exactly one row stays default.
    /// </summary>
    private static void SeedCampuses(SqliteConnection conn)
    {
        using var tx = conn.BeginTransaction();
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT OR IGNORE INTO Campuses (Id, Name, ShortName, Latitude, Longitude, IsDefault, SortOrder)
                VALUES (@id, @name, @shortName, @latitude, @longitude, @isDefault, @sortOrder);
                """;

            var id = cmd.Parameters.Add("@id", SqliteType.Text);
            var name = cmd.Parameters.Add("@name", SqliteType.Text);
            var shortName = cmd.Parameters.Add("@shortName", SqliteType.Text);
            var latitude = cmd.Parameters.Add("@latitude", SqliteType.Real);
            var longitude = cmd.Parameters.Add("@longitude", SqliteType.Real);
            var isDefault = cmd.Parameters.Add("@isDefault", SqliteType.Integer);
            var sortOrder = cmd.Parameters.Add("@sortOrder", SqliteType.Integer);

            foreach (var campus in CampusSeedData.CreateCampuses())
            {
                id.Value = campus.Id;
                name.Value = campus.Name;
                shortName.Value = campus.ShortName;
                latitude.Value = campus.Latitude;
                longitude.Value = campus.Longitude;
                isDefault.Value = campus.IsDefault ? 1 : 0;
                sortOrder.Value = campus.SortOrder;
                cmd.ExecuteNonQuery();
            }
        }

        tx.Commit();
    }

    /// <summary>
    /// BR-LST-06: the search read model joins <c>Users</c> on <c>IsVerified</c>, so demo rooms need a
    /// verified landlord row. <c>INSERT OR IGNORE</c> keeps any real landlord that already owns the id.
    /// </summary>
    private static void SeedDemoLandlordUserIfMissing(SqliteConnection conn)
    {
        const string landlordId = "seed-landlord";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO Users (
                Id, Email, NormalizedEmail, FullName, PasswordHash, Role, AuthProvider,
                SsoSubjectId, PhoneNumber, StudentCode, IsVerified, FailedLoginCount,
                LockoutEndUtc, CreatedAtUtc, UpdatedAtUtc
            ) VALUES (
                @id, @email, @normalizedEmail, @fullName, NULL, 'Landlord', 'Local',
                NULL, @phoneNumber, NULL, 1, 0,
                NULL, @createdAtUtc, @createdAtUtc
            );
            """;
        cmd.Parameters.AddWithValue("@id", landlordId);
        cmd.Parameters.AddWithValue("@email", "seed-landlord@roomtorentradar.local");
        cmd.Parameters.AddWithValue("@normalizedEmail", "seed-landlord@roomtorentradar.local");
        cmd.Parameters.AddWithValue("@fullName", "Seed Landlord");
        cmd.Parameters.AddWithValue("@phoneNumber", "0988123456");
        cmd.Parameters.AddWithValue("@createdAtUtc", "2026-11-01T08:00:00.0000000Z");
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Chèn dữ liệu mẫu 8 phòng khi bảng Rooms còn rỗng (chỉ một lần, chỉ khi SeedDemoRooms bật).
    /// Chạy trong transaction, trong cùng lock khởi tạo schema.
    /// </summary>
    private static void SeedRoomsIfEmpty(SqliteConnection conn)
    {
        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(1) FROM Rooms;";
            var existing = Convert.ToInt64(countCmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            if (existing > 0)
            {
                return;
            }
        }

        using var tx = conn.BeginTransaction();
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT OR IGNORE INTO Rooms (
                    Id, LandlordId, Title, Description, RoomType, MonthlyRentVnd, DepositVnd, AreaM2,
                    MaxOccupants, Furnishing, AvailableFrom, AddressLine, Ward, District, Latitude, Longitude,
                    ElectricityPriceVndPerKwh, WaterPriceVnd, WaterPriceUnit, InternetFeeVnd, ParkingFeeVnd,
                    OtherFeesNote, Status, StatusReason, NeedsReReview, IsSafetyVerified, VerifiedAtUtc,
                    VerifiedByUserId, PublishedAtUtc, LastUpdatedAtUtc, CreatedAtUtc, Version
                ) VALUES (
                    @id, @landlordId, @title, @description, @roomType, @monthlyRentVnd, @depositVnd, @areaM2,
                    @maxOccupants, @furnishing, @availableFrom, @addressLine, @ward, @district, @latitude, @longitude,
                    @electricityPriceVndPerKwh, @waterPriceVnd, @waterPriceUnit, @internetFeeVnd, @parkingFeeVnd,
                    @otherFeesNote, @status, @statusReason, @needsReReview, @isSafetyVerified, @verifiedAtUtc,
                    @verifiedByUserId, @publishedAtUtc, @lastUpdatedAtUtc, @createdAtUtc, @version
                );
                """;

            var parameters = new[]
            {
                "@id", "@landlordId", "@title", "@description", "@roomType", "@monthlyRentVnd", "@depositVnd", "@areaM2",
                "@maxOccupants", "@furnishing", "@availableFrom", "@addressLine", "@ward", "@district", "@latitude", "@longitude",
                "@electricityPriceVndPerKwh", "@waterPriceVnd", "@waterPriceUnit", "@internetFeeVnd", "@parkingFeeVnd",
                "@otherFeesNote", "@status", "@statusReason", "@needsReReview", "@isSafetyVerified", "@verifiedAtUtc",
                "@verifiedByUserId", "@publishedAtUtc", "@lastUpdatedAtUtc", "@createdAtUtc", "@version"
            }.Select(name =>
            {
                var parameter = cmd.CreateParameter();
                parameter.ParameterName = name;
                cmd.Parameters.Add(parameter);
                return parameter;
            }).ToArray();

            foreach (var room in RoomSeedData.CreateRooms())
            {
                parameters[0].Value = room.Id;
                parameters[1].Value = room.LandlordId;
                parameters[2].Value = room.Title;
                parameters[3].Value = (object?)room.Description ?? DBNull.Value;
                parameters[4].Value = room.RoomType.ToString();
                parameters[5].Value = room.MonthlyRentVnd;
                parameters[6].Value = room.DepositVnd;
                parameters[7].Value = room.AreaM2;
                parameters[8].Value = (object?)room.MaxOccupants ?? DBNull.Value;
                parameters[9].Value = (object?)room.Furnishing ?? DBNull.Value;
                parameters[10].Value = room.AvailableFrom.HasValue
                    ? room.AvailableFrom.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : DBNull.Value;
                parameters[11].Value = room.AddressLine;
                parameters[12].Value = room.Ward;
                parameters[13].Value = room.District;
                parameters[14].Value = room.Latitude;
                parameters[15].Value = room.Longitude;
                parameters[16].Value = (object?)room.ElectricityPriceVndPerKwh ?? DBNull.Value;
                parameters[17].Value = (object?)room.WaterPriceVnd ?? DBNull.Value;
                parameters[18].Value = (object?)room.WaterPriceUnit ?? DBNull.Value;
                parameters[19].Value = (object?)room.InternetFeeVnd ?? DBNull.Value;
                parameters[20].Value = (object?)room.ParkingFeeVnd ?? DBNull.Value;
                parameters[21].Value = (object?)room.OtherFeesNote ?? DBNull.Value;
                parameters[22].Value = room.Status.ToString();
                parameters[23].Value = (object?)room.StatusReason ?? DBNull.Value;
                parameters[24].Value = room.NeedsReReview ? 1 : 0;
                parameters[25].Value = room.IsSafetyVerified ? 1 : 0;
                parameters[26].Value = room.VerifiedAtUtc.HasValue
                    ? room.VerifiedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture)
                    : DBNull.Value;
                parameters[27].Value = (object?)room.VerifiedByUserId ?? DBNull.Value;
                parameters[28].Value = room.PublishedAtUtc.HasValue
                    ? room.PublishedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture)
                    : DBNull.Value;
                parameters[29].Value = room.LastUpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture);
                parameters[30].Value = room.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture);
                parameters[31].Value = room.Version;
                cmd.ExecuteNonQuery();
            }
        }

        tx.Commit();
    }

    public void Dispose()
    {
        _keepAliveInMemoryConnection?.Dispose();
    }
}
