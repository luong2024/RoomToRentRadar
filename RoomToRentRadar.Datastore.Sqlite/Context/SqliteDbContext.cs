using System.Globalization;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Seed;
using SQLitePCL;

namespace RoomToRentRadar.Datastore.Sqlite.Context;

public sealed class SqliteDbContext : IDisposable
{
    /// <summary>
    /// Columns that must exist on <c>Rooms</c> for the ERD §4.4 schema. A pre-Task-3 database
    /// (Phase 0 schema) lacks several of them; see <see cref="MigrateLegacyRoomsIfNeeded"/>.
    /// </summary>
    private static readonly string[] RequiredRoomColumns =
    [
        "Id", "LandlordId", "Title", "Description", "RoomType", "MonthlyRentVnd", "DepositVnd", "AreaM2",
        "MaxOccupants", "Furnishing", "AvailableFrom", "AddressLine", "Ward", "District", "Latitude", "Longitude",
        "ElectricityPriceVndPerKwh", "WaterPriceVnd", "WaterPriceUnit", "InternetFeeVnd", "ParkingFeeVnd",
        "OtherFeesNote", "Status", "StatusReason", "NeedsReReview", "IsSafetyVerified", "VerifiedAtUtc",
        "VerifiedByUserId", "PublishedAtUtc", "LastUpdatedAtUtc", "CreatedAtUtc", "Version"
    ];

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
            }

            try
            {
                MigrateLegacyRoomsIfNeeded(conn);

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = """
                        CREATE TABLE IF NOT EXISTS Users (
                            Id TEXT PRIMARY KEY NOT NULL,
                            Email TEXT NOT NULL,
                            NormalizedEmail TEXT NOT NULL UNIQUE,
                            FullName TEXT NOT NULL,
                            PasswordHash TEXT NULL,
                            Role TEXT NOT NULL,
                            AuthProvider TEXT NOT NULL,
                            SsoSubjectId TEXT NULL,
                            PhoneNumber TEXT NULL,
                            HouseName TEXT NULL,
                            HouseAddress TEXT NULL,
                            StudentCode TEXT NULL,
                            IsVerified INTEGER NOT NULL,
                            FailedLoginCount INTEGER NOT NULL,
                            LockoutEndUtc TEXT NULL,
                            CreatedAtUtc TEXT NOT NULL,
                            UpdatedAtUtc TEXT NOT NULL
                        );

                        CREATE TABLE IF NOT EXISTS UserRefreshTokens (
                            TokenHash TEXT PRIMARY KEY NOT NULL,
                            UserId TEXT NOT NULL,
                            JwtId TEXT NOT NULL,
                            CreatedAtUtc TEXT NOT NULL,
                            ExpiresAtUtc TEXT NOT NULL,
                            RevokedAtUtc TEXT NULL,
                            ReplacedByTokenHash TEXT NULL,
                            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
                        );

                        CREATE TABLE IF NOT EXISTS AuditLogs (
                            Id TEXT PRIMARY KEY NOT NULL,
                            UserId TEXT NULL,
                            UserEmail TEXT NULL,
                            Role TEXT NOT NULL,
                            Action TEXT NOT NULL,
                            EntityType TEXT NOT NULL,
                            EntityId TEXT NULL,
                            DetailsJson TEXT NULL,
                            IpAddress TEXT NULL,
                            UserAgent TEXT NULL,
                            StatusCode INTEGER NULL,
                            DurationMs INTEGER NULL,
                            IsSuccess INTEGER NOT NULL,
                            ErrorMessage TEXT NULL,
                            TimestampUtc TEXT NOT NULL
                        );

                        -- ERD §4.1: campus reference data (seeded in CampusSeedData, BR-CMP-01..03).
                        CREATE TABLE IF NOT EXISTS Campuses (
                            Id TEXT PRIMARY KEY NOT NULL,
                            Name TEXT NOT NULL,
                            ShortName TEXT NOT NULL,
                            Latitude REAL NOT NULL,
                            Longitude REAL NOT NULL,
                            IsDefault INTEGER NOT NULL DEFAULT 0,
                            SortOrder INTEGER NOT NULL DEFAULT 0
                        );

                        -- ERD §4.4: full Rooms table (33 columns). ConcurrencyToken is derived from
                        -- Version (ERD §1), never stored as a second source of truth.
                        CREATE TABLE IF NOT EXISTS Rooms (
                            Id TEXT PRIMARY KEY NOT NULL,
                            LandlordId TEXT NOT NULL,
                            Title TEXT NOT NULL,
                            Description TEXT NULL,
                            RoomType TEXT NOT NULL,
                            MonthlyRentVnd INTEGER NOT NULL,
                            DepositVnd INTEGER NOT NULL,
                            AreaM2 REAL NOT NULL,
                            MaxOccupants INTEGER NULL,
                            Furnishing TEXT NULL,
                            AvailableFrom TEXT NULL,
                            AddressLine TEXT NOT NULL,
                            Ward TEXT NOT NULL,
                            District TEXT NOT NULL,
                            Latitude REAL NOT NULL,
                            Longitude REAL NOT NULL,
                            ElectricityPriceVndPerKwh INTEGER NULL,
                            WaterPriceVnd INTEGER NULL,
                            WaterPriceUnit TEXT NULL,
                            InternetFeeVnd INTEGER NULL,
                            ParkingFeeVnd INTEGER NULL,
                            OtherFeesNote TEXT NULL,
                            Status TEXT NOT NULL,
                            StatusReason TEXT NULL,
                            NeedsReReview INTEGER NOT NULL DEFAULT 0,
                            IsSafetyVerified INTEGER NOT NULL DEFAULT 0,
                            VerifiedAtUtc TEXT NULL,
                            VerifiedByUserId TEXT NULL,
                            PublishedAtUtc TEXT NULL,
                            LastUpdatedAtUtc TEXT NOT NULL,
                            CreatedAtUtc TEXT NOT NULL,
                            Version INTEGER NOT NULL DEFAULT 1
                        );

                        CREATE INDEX IF NOT EXISTS IX_Users_NormalizedEmail ON Users(NormalizedEmail);
                        CREATE INDEX IF NOT EXISTS IX_UserRefreshTokens_UserId ON UserRefreshTokens(UserId);
                        CREATE INDEX IF NOT EXISTS IX_AuditLogs_TimestampUtc ON AuditLogs(TimestampUtc DESC);
                        CREATE INDEX IF NOT EXISTS IX_AuditLogs_UserId ON AuditLogs(UserId);
                        CREATE INDEX IF NOT EXISTS IX_AuditLogs_Action ON AuditLogs(Action);

                        -- ERD §4.4 indexes (ADR-006 bounding-box prefilter + search/sort).
                        CREATE INDEX IF NOT EXISTS IX_Rooms_Status_Lat_Lng ON Rooms(Status, Latitude, Longitude);
                        CREATE INDEX IF NOT EXISTS IX_Rooms_Status_Rent ON Rooms(Status, MonthlyRentVnd);
                        CREATE INDEX IF NOT EXISTS IX_Rooms_LandlordId ON Rooms(LandlordId);
                        CREATE INDEX IF NOT EXISTS IX_Rooms_Status_PublishedAtUtc ON Rooms(Status, PublishedAtUtc);
                        CREATE INDEX IF NOT EXISTS IX_Rooms_NeedsReReview ON Rooms(NeedsReReview) WHERE NeedsReReview = 1;
                        """;
                    cmd.ExecuteNonQuery();
                }

                // BR-AUTH-03 / H2: Ensure HouseName and HouseAddress columns exist on existing databases.
                EnsureUserColumnsExist(conn);

                // Campuses is reference data: always seeded, in every environment (H4 / ADR-002).
                SeedCampuses(conn);

                // ERD §1: ConcurrencyToken = Version.ToString(), so the column is redundant on
                // databases created by older builds; drop it rather than keeping two sources of truth.
                DropLegacyConcurrencyTokenColumnIfPresent(conn);

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

    /// <summary>
    /// Ruling #10: <c>CREATE TABLE IF NOT EXISTS</c> never upgrades an existing table, so a database
    /// written by an older build keeps its truncated <c>Rooms</c> schema. If the table exists but is
    /// missing ERD §4.4 columns, rename it to <c>Rooms_Legacy</c> (data preserved, never dropped) so
    /// the new schema can be created.
    /// </summary>
    private static void MigrateLegacyRoomsIfNeeded(SqliteConnection conn)
    {
        HashSet<string> existingColumns;
        using (var tableInfo = conn.CreateCommand())
        {
            tableInfo.CommandText = "PRAGMA table_info(Rooms);";
            existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var reader = tableInfo.ExecuteReader();
            while (reader.Read())
            {
                existingColumns.Add(reader.GetString(1));
            }
        }

        // Empty set → table does not exist yet, nothing to migrate.
        if (existingColumns.Count == 0 || RequiredRoomColumns.All(existingColumns.Contains))
        {
            return;
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var legacyTable = $"Rooms_Legacy_{timestamp}";

        // Never overwrite an earlier legacy copy (e.g. two migrations in the same second).
        var suffix = 1;
        while (TableExists(conn, legacyTable))
        {
            legacyTable = $"Rooms_Legacy_{timestamp}_{suffix++}";
        }

        using var rename = conn.CreateCommand();
        rename.CommandText = $"ALTER TABLE Rooms RENAME TO \"{legacyTable}\";";
        rename.ExecuteNonQuery();
    }

    private static bool TableExists(SqliteConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = @name;";
        cmd.Parameters.AddWithValue("@name", tableName);
        return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }

    /// <summary>
    /// ERD §1: the concurrency token is <c>Version.ToString()</c>, so a stored
    /// <c>ConcurrencyToken</c> column (written by older builds) is redundant and blocks INSERTs that
    /// no longer bind it. Drop it only when present, so fresh databases are untouched.
    /// </summary>
    private static void DropLegacyConcurrencyTokenColumnIfPresent(SqliteConnection conn)
    {
        var hasColumn = false;
        using (var tableInfo = conn.CreateCommand())
        {
            tableInfo.CommandText = "PRAGMA table_info(Rooms);";
            using var reader = tableInfo.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), "ConcurrencyToken", StringComparison.OrdinalIgnoreCase))
                {
                    hasColumn = true;
                    break;
                }
            }
        }

        if (!hasColumn)
        {
            return;
        }

        using var alter = conn.CreateCommand();
        alter.CommandText = "ALTER TABLE Rooms DROP COLUMN ConcurrencyToken;";
        alter.ExecuteNonQuery();
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
    /// BR-AUTH-03 / H2: CREATE TABLE IF NOT EXISTS không cập nhật bảng Users khi database đã tồn tại.
    /// Phương thức kiểm tra PRAGMA table_info và tự động bổ sung cột HouseName, HouseAddress nếu thiếu.
    /// </summary>
    private static void EnsureUserColumnsExist(SqliteConnection conn)
    {
        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var tableInfo = conn.CreateCommand())
        {
            tableInfo.CommandText = "PRAGMA table_info(Users);";
            using var reader = tableInfo.ExecuteReader();
            while (reader.Read())
            {
                existingColumns.Add(reader.GetString(1));
            }
        }

        if (existingColumns.Count == 0)
        {
            return;
        }

        if (!existingColumns.Contains("HouseName"))
        {
            using var alterCmd = conn.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE Users ADD COLUMN HouseName TEXT NULL;";
            alterCmd.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("HouseAddress"))
        {
            using var alterCmd = conn.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE Users ADD COLUMN HouseAddress TEXT NULL;";
            alterCmd.ExecuteNonQuery();
        }
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
                SsoSubjectId, PhoneNumber, HouseName, HouseAddress, StudentCode, IsVerified, FailedLoginCount,
                LockoutEndUtc, CreatedAtUtc, UpdatedAtUtc
            ) VALUES (
                @id, @email, @normalizedEmail, @fullName, NULL, 'Landlord', 'Local',
                NULL, @phoneNumber, @houseName, @houseAddress, NULL, 1, 0,
                NULL, @createdAtUtc, @createdAtUtc
            );
            """;
        cmd.Parameters.AddWithValue("@id", landlordId);
        cmd.Parameters.AddWithValue("@email", "seed-landlord@roomtorentradar.local");
        cmd.Parameters.AddWithValue("@normalizedEmail", "seed-landlord@roomtorentradar.local");
        cmd.Parameters.AddWithValue("@fullName", "Seed Landlord");
        cmd.Parameters.AddWithValue("@phoneNumber", "0987654321");
        cmd.Parameters.AddWithValue("@houseName", "Nhà trọ Mẫu Thái Nguyên");
        cmd.Parameters.AddWithValue("@houseAddress", "Số 123 Đường Z115, TP. Thái Nguyên");
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
