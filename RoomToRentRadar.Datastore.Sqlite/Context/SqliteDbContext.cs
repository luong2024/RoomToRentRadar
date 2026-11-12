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
                using var cmd = conn.CreateCommand();
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

                    CREATE INDEX IF NOT EXISTS IX_Users_NormalizedEmail ON Users(NormalizedEmail);
                    CREATE INDEX IF NOT EXISTS IX_UserRefreshTokens_UserId ON UserRefreshTokens(UserId);
                    CREATE INDEX IF NOT EXISTS IX_AuditLogs_TimestampUtc ON AuditLogs(TimestampUtc DESC);
                    CREATE INDEX IF NOT EXISTS IX_AuditLogs_UserId ON AuditLogs(UserId);
                    CREATE INDEX IF NOT EXISTS IX_AuditLogs_Action ON AuditLogs(Action);

                    -- Temporary Phase 0 table: Task 3 replaces it with the full ERD §4.4 schema
                    -- (Campuses table, Status/RoomType as TEXT, concurrency columns).
                    CREATE TABLE IF NOT EXISTS Rooms (
                        Id TEXT PRIMARY KEY NOT NULL,
                        LandlordId TEXT NOT NULL,
                        Title TEXT NOT NULL,
                        RoomType TEXT NOT NULL,
                        MonthlyRentVnd INTEGER NOT NULL,
                        DepositVnd INTEGER NOT NULL,
                        AreaM2 REAL NOT NULL,
                        AddressLine TEXT NOT NULL,
                        Ward TEXT NOT NULL,
                        District TEXT NOT NULL,
                        Latitude REAL NOT NULL,
                        Longitude REAL NOT NULL,
                        Status TEXT NOT NULL,
                        StatusReason TEXT NULL,
                        VerifiedAtUtc TEXT NULL,
                        PublishedAtUtc TEXT NULL,
                        LastUpdatedAtUtc TEXT NOT NULL,
                        CreatedAtUtc TEXT NOT NULL,
                        Version INTEGER NOT NULL DEFAULT 1,
                        ConcurrencyToken TEXT NOT NULL
                    );

                    CREATE INDEX IF NOT EXISTS IX_Rooms_Status_Rent ON Rooms(Status, MonthlyRentVnd);
                    """;
                cmd.ExecuteNonQuery();

                if (_settings.SeedDemoRooms)
                {
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
    /// Chèn dữ liệu mẫu 8 phòng khi bảng Rooms còn rỗng (chỉ một lần).
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
        using (var insertCmd = conn.CreateCommand())
        {
            insertCmd.Transaction = tx;
            insertCmd.CommandText = """
                INSERT OR IGNORE INTO Rooms (
                    Id, LandlordId, Title, RoomType, MonthlyRentVnd, DepositVnd, AreaM2,
                    AddressLine, Ward, District, Latitude, Longitude, Status, StatusReason,
                    VerifiedAtUtc, PublishedAtUtc, LastUpdatedAtUtc, CreatedAtUtc, Version, ConcurrencyToken
                ) VALUES (
                    @id, @landlordId, @title, @roomType, @monthlyRentVnd, @depositVnd, @areaM2,
                    @addressLine, @ward, @district, @latitude, @longitude, @status, @statusReason,
                    @verifiedAtUtc, @publishedAtUtc, @lastUpdatedAtUtc, @createdAtUtc, @version, @concurrencyToken
                );
                """;

            var parameters = new[]
            {
                "@id", "@landlordId", "@title", "@roomType", "@monthlyRentVnd", "@depositVnd", "@areaM2",
                "@addressLine", "@ward", "@district", "@latitude", "@longitude", "@status", "@statusReason",
                "@verifiedAtUtc", "@publishedAtUtc", "@lastUpdatedAtUtc", "@createdAtUtc", "@version", "@concurrencyToken"
            }.Select(name =>
            {
                var parameter = insertCmd.CreateParameter();
                parameter.ParameterName = name;
                insertCmd.Parameters.Add(parameter);
                return parameter;
            }).ToArray();

            foreach (var room in RoomSeedData.CreateRooms())
            {
                parameters[0].Value = room.Id;
                parameters[1].Value = room.LandlordId;
                parameters[2].Value = room.Title;
                parameters[3].Value = room.RoomType.ToString();
                parameters[4].Value = room.MonthlyRentVnd;
                parameters[5].Value = room.DepositVnd;
                parameters[6].Value = room.AreaM2;
                parameters[7].Value = room.AddressLine;
                parameters[8].Value = room.Ward;
                parameters[9].Value = room.District;
                parameters[10].Value = room.Latitude;
                parameters[11].Value = room.Longitude;
                parameters[12].Value = room.Status.ToString();
                parameters[13].Value = (object?)room.StatusReason ?? DBNull.Value;
                parameters[14].Value = room.VerifiedAtUtc.HasValue
                    ? room.VerifiedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture)
                    : DBNull.Value;
                parameters[15].Value = room.PublishedAtUtc.HasValue
                    ? room.PublishedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture)
                    : DBNull.Value;
                parameters[16].Value = room.LastUpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture);
                parameters[17].Value = room.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture);
                parameters[18].Value = room.Version;
                parameters[19].Value = room.ConcurrencyToken;
                insertCmd.ExecuteNonQuery();
            }
        }

        tx.Commit();
    }

    public void Dispose()
    {
        _keepAliveInMemoryConnection?.Dispose();
    }
}

