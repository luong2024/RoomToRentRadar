using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
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

                    CREATE TABLE IF NOT EXISTS RoomPhotos (
                        Id TEXT PRIMARY KEY NOT NULL,
                        RoomId TEXT NOT NULL,
                        FileName TEXT NOT NULL,
                        Url TEXT NOT NULL,
                        ContentType TEXT NOT NULL,
                        SizeBytes INTEGER NOT NULL,
                        UploadedByUserId TEXT NOT NULL,
                        UploadedAtUtc TEXT NOT NULL
                    );

                    CREATE INDEX IF NOT EXISTS IX_RoomPhotos_RoomId ON RoomPhotos(RoomId);
                    """;
                cmd.ExecuteNonQuery();
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

    public void Dispose()
    {
        _keepAliveInMemoryConnection?.Dispose();
    }
}

