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

                // Configured accounts (Admin:SeedAccounts, plus DemoData:Accounts in Development)
                // run before SeedDemoLandlordUserIfMissing so configured credentials take precedence.
                SeedConfiguredAccounts(conn, _settings.SeedAccounts);

                if (_settings.SeedDemoRooms)
                {
                    // BR-LST-06: search only shows rooms whose landlord is verified, so the demo
                    // landlord must exist (and be verified) before the demo rooms are inserted.
                    SeedDemoLandlordUserIfMissing(conn);
                    SeedRoomsIfEmpty(conn);

                    if (_settings.SeedExtendedDemoData)
                    {
                        DemoDataSeeder.Seed(conn);
                    }
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

    private static void SeedConfiguredAccounts(
        SqliteConnection conn,
        IReadOnlyList<SeedAccount>? seedAccounts)
    {
        if (seedAccounts is not { Count: > 0 })
        {
            return;
        }

        const string createdAtUtc = "2026-11-01T08:00:00.0000000Z";

        using var tx = conn.BeginTransaction();
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT OR IGNORE INTO Users (
                    Id, Email, NormalizedEmail, FullName, PasswordHash, Role, AuthProvider,
                    SsoSubjectId, PhoneNumber, StudentCode, IsVerified, FailedLoginCount,
                    LockoutEndUtc, CreatedAtUtc, UpdatedAtUtc
                ) VALUES (
                    @id, @email, @normalizedEmail, @fullName, @passwordHash, @role, 'Local',
                    NULL, @phoneNumber, @studentCode, @isVerified, 0,
                    NULL, @createdAtUtc, @createdAtUtc
                );
                """;

            var idParam = cmd.Parameters.Add("@id", SqliteType.Text);
            var emailParam = cmd.Parameters.Add("@email", SqliteType.Text);
            var normalizedEmailParam = cmd.Parameters.Add("@normalizedEmail", SqliteType.Text);
            var fullNameParam = cmd.Parameters.Add("@fullName", SqliteType.Text);
            var passwordHashParam = cmd.Parameters.Add("@passwordHash", SqliteType.Text);
            var roleParam = cmd.Parameters.Add("@role", SqliteType.Text);
            var phoneNumberParam = cmd.Parameters.Add("@phoneNumber", SqliteType.Text);
            var studentCodeParam = cmd.Parameters.Add("@studentCode", SqliteType.Text);
            var isVerifiedParam = cmd.Parameters.Add("@isVerified", SqliteType.Integer);
            cmd.Parameters.AddWithValue("@createdAtUtc", createdAtUtc);

            foreach (var account in seedAccounts)
            {
                var trimmedEmail = account.Email.Trim();
                idParam.Value = account.Id.Trim();
                emailParam.Value = trimmedEmail;
                normalizedEmailParam.Value = trimmedEmail.ToLowerInvariant();
                fullNameParam.Value = account.FullName.Trim();
                passwordHashParam.Value = account.PasswordHash;
                roleParam.Value = account.Role.ToString();
                phoneNumberParam.Value = (object?)account.PhoneNumber ?? DBNull.Value;
                studentCodeParam.Value = (object?)account.StudentCode ?? DBNull.Value;
                isVerifiedParam.Value = account.IsVerified ? 1 : 0;
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
                NULL, NULL, NULL, 1, 0,
                NULL, @createdAtUtc, @createdAtUtc
            );
            """;
        cmd.Parameters.AddWithValue("@id", landlordId);
        cmd.Parameters.AddWithValue("@email", "seed-landlord@roomtorentradar.local");
        cmd.Parameters.AddWithValue("@normalizedEmail", "seed-landlord@roomtorentradar.local");
        cmd.Parameters.AddWithValue("@fullName", "Seed Landlord");
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
        foreach (var room in RoomSeedData.CreateRooms())
        {
            DemoRoomRowWriter.InsertOrIgnore(conn, tx, room);
        }

        tx.Commit();
    }

    public void Dispose()
    {
        _keepAliveInMemoryConnection?.Dispose();
    }
}
