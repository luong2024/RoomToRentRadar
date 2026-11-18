using System.Globalization;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Context;

namespace RoomToRentRadarUnitTests.Infrastructure;

/// <summary>
/// T00.6: versioned SQLite migrations tracked via PRAGMA user_version.
/// </summary>
[TestFixture]
public class SqliteMigratorTests
{
    private static readonly string[] BaselineTableNames =
    [
        "AuditLogs", "Campuses", "HouseRuleVersions", "RoomAmenities", "RoomSafetyFeatures", "Rooms", "UserRefreshTokens", "Users"
    ];

    private static readonly string[] BaselineIndexNames =
    [
        "IX_AuditLogs_Action", "IX_AuditLogs_TimestampUtc", "IX_AuditLogs_UserId",
        "IX_HouseRuleVersions_RoomId", "IX_Rooms_LandlordId", "IX_Rooms_NeedsReReview",
        "IX_Rooms_Status_Lat_Lng", "IX_Rooms_Status_PublishedAtUtc", "IX_Rooms_Status_Rent",
        "IX_UserRefreshTokens_UserId", "IX_Users_NormalizedEmail"
    ];

    private static readonly string[] RoomColumnNames =
    [
        "Id", "LandlordId", "Title", "Description", "RoomType", "MonthlyRentVnd", "DepositVnd", "AreaM2",
        "MaxOccupants", "Furnishing", "AvailableFrom", "AddressLine", "Ward", "District", "Latitude", "Longitude",
        "ElectricityPriceVndPerKwh", "WaterPriceVnd", "WaterPriceUnit", "InternetFeeVnd", "ParkingFeeVnd",
        "OtherFeesNote", "Status", "StatusReason", "NeedsReReview", "IsSafetyVerified", "VerifiedAtUtc",
        "VerifiedByUserId", "PublishedAtUtc", "LastUpdatedAtUtc", "CreatedAtUtc", "Version"
    ];

    [OneTimeSetUp]
    public void InitializeSqliteProvider() => SQLitePCL.Batteries_V2.Init();

    [Test]
    public void Migrate_FreshDatabase_CreatesBaselineSchema()
    {
        using var connection = OpenMemoryConnection();

        var applied = SqliteMigrator.Migrate(connection);

        Assert.Multiple(() =>
        {
            Assert.That(applied, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(ReadNames(connection, "table"), Is.EquivalentTo(BaselineTableNames));
            Assert.That(ReadNames(connection, "index"), Is.EquivalentTo(BaselineIndexNames));

            var columns = ReadRoomColumns(connection);
            Assert.That(columns, Has.Count.EqualTo(32));
            Assert.That(columns, Is.EquivalentTo(RoomColumnNames));
            Assert.That(columns, Does.Not.Contain("ConcurrencyToken"),
                "ConcurrencyToken là Version.ToString() (ERD §1) — không được lưu thành cột riêng.");
            Assert.That(ReadUserVersion(connection), Is.EqualTo(LatestVersion));
        });
    }

    [Test]
    public void Migrate_AlreadyMigratedDatabase_AppliesNothing()
    {
        using var connection = OpenMemoryConnection();
        SqliteMigrator.Migrate(connection);

        var applied = SqliteMigrator.Migrate(connection);

        Assert.Multiple(() =>
        {
            Assert.That(applied, Is.Empty);
            Assert.That(ReadUserVersion(connection), Is.EqualTo(LatestVersion));
        });
    }

    [Test]
    public void Migrate_NewerScriptAvailable_AppliesOnlyNewerScript()
    {
        using var connection = OpenMemoryConnection();
        SqliteMigrator.Migrate(connection);
        var nextVersion = LatestVersion + 1;
        var scripts = SqliteMigrator.LoadEmbedded()
            .Append(new SqliteMigrator.MigrationScript(nextVersion, $"Migrations/{nextVersion:D4}_fake.sql",
                "CREATE TABLE FakeTwo (Id TEXT PRIMARY KEY NOT NULL);"))
            .ToList();

        var applied = SqliteMigrator.Migrate(connection, scripts);

        Assert.Multiple(() =>
        {
            Assert.That(applied, Is.EqualTo(new[] { nextVersion }));
            Assert.That(ReadNames(connection, "table"), Does.Contain("FakeTwo"));
            Assert.That(ReadUserVersion(connection), Is.EqualTo(nextVersion));
        });
    }

    [Test]
    public void Migrate_OutOfOrderScripts_AppliesInAscendingOrder()
    {
        using var connection = OpenMemoryConnection();
        var scripts = new[]
        {
            new SqliteMigrator.MigrationScript(3, "Migrations/0003_third.sql",
                "CREATE TABLE Third (Id TEXT PRIMARY KEY NOT NULL);"),
            new SqliteMigrator.MigrationScript(1, "Migrations/0001_first.sql",
                "CREATE TABLE First (Id TEXT PRIMARY KEY NOT NULL, Name TEXT NOT NULL);"),
            // Depends on script 0001: fails if the list order were used instead of the version order.
            new SqliteMigrator.MigrationScript(2, "Migrations/0002_second.sql",
                "CREATE INDEX IX_First_Name ON First(Name);")
        };

        var applied = SqliteMigrator.Migrate(connection, scripts);

        Assert.Multiple(() =>
        {
            Assert.That(applied, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(ReadNames(connection, "index"), Does.Contain("IX_First_Name"));
            Assert.That(ReadUserVersion(connection), Is.EqualTo(3));
        });
    }

    [Test]
    public void Migrate_FailingScript_RollsBackSchemaAndVersion()
    {
        using var connection = OpenMemoryConnection();
        SqliteMigrator.Migrate(connection);
        var nextVersion = LatestVersion + 1;
        var scripts = SqliteMigrator.LoadEmbedded()
            .Append(new SqliteMigrator.MigrationScript(nextVersion, $"Migrations/{nextVersion:D4}_broken.sql",
                "CREATE TABLE ShouldNotExist (Id TEXT PRIMARY KEY NOT NULL); INSERT INTO MissingTable (Id) VALUES ('x');"))
            .ToList();

        var exception = Assert.Throws<InvalidOperationException>(() => SqliteMigrator.Migrate(connection, scripts));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain(nextVersion.ToString("D4", CultureInfo.InvariantCulture)));
            Assert.That(ReadUserVersion(connection), Is.EqualTo(LatestVersion));
            Assert.That(ReadNames(connection, "table"), Does.Not.Contain("ShouldNotExist"));
        });
    }

    [Test]
    public void Migrate_DuplicateScriptVersions_Throws()
    {
        using var connection = OpenMemoryConnection();
        var scripts = new[]
        {
            new SqliteMigrator.MigrationScript(1, "Migrations/0001_a.sql",
                "CREATE TABLE A (Id TEXT PRIMARY KEY NOT NULL);"),
            new SqliteMigrator.MigrationScript(1, "Migrations/0001_b.sql",
                "CREATE TABLE B (Id TEXT PRIMARY KEY NOT NULL);")
        };

        Assert.Throws<InvalidOperationException>(() => SqliteMigrator.Migrate(connection, scripts));

        Assert.That(ReadUserVersion(connection), Is.EqualTo(0));
    }

    [Test]
    public void Migrate_PreVersionedDatabaseWithTables_Throws()
    {
        using var connection = OpenMemoryConnection();
        Execute(connection, "CREATE TABLE Legacy (Id TEXT PRIMARY KEY NOT NULL);");

        var exception = Assert.Throws<InvalidOperationException>(() => SqliteMigrator.Migrate(connection));

        Assert.That(exception!.Message, Does.Contain("roomtorentradar.db"),
            "Message must tell the developer to delete the local roomtorentradar.db once.");
    }

    [Test]
    public void Migrate_DatabaseNewerThanKnownScripts_Throws()
    {
        using var connection = OpenMemoryConnection();
        Execute(connection, "PRAGMA user_version = 99;");

        Assert.Throws<InvalidOperationException>(() => SqliteMigrator.Migrate(connection));
    }

    [Test]
    public void LoadEmbedded_WithEmbeddedBaseline_ReturnsUniqueAscendingVersions()
    {
        var scripts = SqliteMigrator.LoadEmbedded();

        Assert.Multiple(() =>
        {
            Assert.That(scripts, Is.Not.Empty);
            Assert.That(scripts[0].Version, Is.EqualTo(1));
            Assert.That(scripts[0].Name, Is.EqualTo("Migrations/0001_baseline.sql"));
            Assert.That(scripts.Select(script => script.Version), Is.Ordered.Ascending);
            Assert.That(scripts.Select(script => script.Version), Is.Unique);
            Assert.That(scripts[0].Sql, Does.Contain("CREATE TABLE Rooms"));
            Assert.That(scripts[0].Sql, Does.Not.Contain("IF NOT EXISTS"));
        });
    }

    [Test]
    public void Migrate_ExistingUsersAndRefreshTokens_PreservedWithDefaultsApplied()
    {
        using var connection = OpenMemoryConnection();
        MigrateToVersionOne(connection);
        InsertIctuCampus(connection);
        InsertVersionOneUser(connection, "user-old");
        Execute(connection, """
            INSERT INTO UserRefreshTokens (TokenHash, UserId, JwtId, CreatedAtUtc, ExpiresAtUtc)
            VALUES ('hash-old', 'user-old', 'jti-old', '2026-11-15T08:00:00.0000000Z', '2026-11-22T08:00:00.0000000Z');
            """);
        Assert.That(ReadScalarLong(connection, "PRAGMA foreign_keys;"), Is.EqualTo(1));

        var applied = SqliteMigrator.Migrate(connection);

        Assert.Multiple(() =>
        {
            Assert.That(applied, Is.EqualTo(new[] { 2, 3 }));
            Assert.That(ReadScalarLong(connection, "SELECT COUNT(1) FROM Users;"), Is.EqualTo(1));
            Assert.That(ReadScalarLong(connection, "SELECT COUNT(1) FROM UserRefreshTokens;"), Is.EqualTo(1));
            Assert.That(ReadScalarText(connection, "SELECT SelectedCampusId FROM Users WHERE Id = 'user-old';"), Is.EqualTo("ICTU"));
            Assert.That(ReadScalarText(connection, "SELECT PreferredLanguage FROM Users WHERE Id = 'user-old';"), Is.EqualTo("en"));
            Assert.That(ReadScalarLong(connection, "SELECT Version FROM Users WHERE Id = 'user-old';"), Is.EqualTo(1));
            Assert.That(ReadUserVersion(connection), Is.EqualTo(3));
        });
    }

    [Test]
    public void Migrate_UsersDelta_EnforcesCheckForeignKeyAndNotNull()
    {
        using var connection = OpenMemoryConnection();
        SqliteMigrator.Migrate(connection);
        InsertIctuCampus(connection);
        InsertVersionOneUser(connection, "user-delta");

        var check = Assert.Throws<SqliteException>(
            () => Execute(connection, "UPDATE Users SET PreferredLanguage = 'fr' WHERE Id = 'user-delta';"));
        Assert.That(check!.SqliteExtendedErrorCode, Is.EqualTo(275), "CHECK constraint on PreferredLanguage.");

        var foreignKey = Assert.Throws<SqliteException>(
            () => Execute(connection, "UPDATE Users SET SelectedCampusId = 'NOPE' WHERE Id = 'user-delta';"));
        Assert.That(foreignKey!.SqliteExtendedErrorCode, Is.EqualTo(787), "FK constraint on SelectedCampusId.");

        var campusNotNull = Assert.Throws<SqliteException>(
            () => Execute(connection, "UPDATE Users SET SelectedCampusId = NULL WHERE Id = 'user-delta';"));
        Assert.That(campusNotNull!.SqliteExtendedErrorCode, Is.EqualTo(1299), "NOT NULL constraint on SelectedCampusId.");

        var versionNotNull = Assert.Throws<SqliteException>(
            () => Execute(connection, "UPDATE Users SET Version = NULL WHERE Id = 'user-delta';"));
        Assert.That(versionNotNull!.SqliteExtendedErrorCode, Is.EqualTo(1299), "NOT NULL constraint on Version.");
    }

    [Test]
    public void Migrate_UsersDelta_InsertWithoutNewColumns_GetsDefaults()
    {
        using var connection = OpenMemoryConnection();
        SqliteMigrator.Migrate(connection);
        InsertIctuCampus(connection);

        InsertVersionOneUser(connection, "user-defaults");

        Assert.Multiple(() =>
        {
            Assert.That(ReadScalarText(connection, "SELECT SelectedCampusId FROM Users WHERE Id = 'user-defaults';"), Is.EqualTo("ICTU"));
            Assert.That(ReadScalarText(connection, "SELECT PreferredLanguage FROM Users WHERE Id = 'user-defaults';"), Is.EqualTo("en"));
            Assert.That(ReadScalarLong(connection, "SELECT Version FROM Users WHERE Id = 'user-defaults';"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Migrate_ForeignKeysOnBeforeMigration_AreOnAfterwards()
    {
        using var connection = OpenMemoryConnection();
        Assert.That(ReadScalarLong(connection, "PRAGMA foreign_keys;"), Is.EqualTo(1));

        SqliteMigrator.Migrate(connection);

        Assert.That(ReadScalarLong(connection, "PRAGMA foreign_keys;"), Is.EqualTo(1));
    }

    [Test]
    public void Migrate_ForeignKeysOffBeforeMigration_StayOffAfterwards()
    {
        using var connection = OpenMemoryConnection();
        Execute(connection, "PRAGMA foreign_keys = OFF;");
        Assert.That(ReadScalarLong(connection, "PRAGMA foreign_keys;"), Is.EqualTo(0));

        SqliteMigrator.Migrate(connection);

        Assert.That(ReadScalarLong(connection, "PRAGMA foreign_keys;"), Is.EqualTo(0));
    }

    [Test]
    public void Migrate_ScriptLeavesForeignKeyViolation_RollsBackAndKeepsVersion()
    {
        using var connection = OpenMemoryConnection();
        MigrateToVersionOne(connection);
        var nextVersion = LatestVersion + 1;
        var scripts = SqliteMigrator.LoadEmbedded()
            .Append(new SqliteMigrator.MigrationScript(nextVersion, $"Migrations/{nextVersion:D4}_orphan.sql",
                "CREATE TABLE Parent (Id TEXT PRIMARY KEY NOT NULL); CREATE TABLE Child (Id TEXT PRIMARY KEY NOT NULL, ParentId TEXT NOT NULL REFERENCES Parent(Id)); INSERT INTO Child (Id, ParentId) VALUES ('c1', 'missing');"))
            .ToList();

        var exception = Assert.Throws<InvalidOperationException>(() => SqliteMigrator.Migrate(connection, scripts));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("foreign_key_check"));
            Assert.That(ReadUserVersion(connection), Is.EqualTo(LatestVersion));
            Assert.That(ReadNames(connection, "table"), Does.Not.Contain("Parent"));
            Assert.That(ReadNames(connection, "table"), Does.Not.Contain("Child"));
            Assert.That(ReadScalarLong(connection, "PRAGMA foreign_keys;"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Migrate_UsersExistButCampusesEmpty_RollsBackWithForeignKeyMessage()
    {
        using var connection = OpenMemoryConnection();
        MigrateToVersionOne(connection);
        InsertVersionOneUser(connection, "user-no-campus");

        var exception = Assert.Throws<InvalidOperationException>(() => SqliteMigrator.Migrate(connection));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("0002"));
            Assert.That(exception.Message, Does.Contain("foreign_key_check"));
            Assert.That(ReadUserVersion(connection), Is.EqualTo(1));
            Assert.That(ReadScalarLong(connection, "SELECT COUNT(1) FROM pragma_table_info('Users') WHERE name = 'SelectedCampusId';"), Is.EqualTo(0));
        });
    }

    [Test]
    public void Migrate_FailingScript_StillRestoresForeignKeys()
    {
        using var connection = OpenMemoryConnection();
        Assert.That(ReadScalarLong(connection, "PRAGMA foreign_keys;"), Is.EqualTo(1));
        var nextVersion = LatestVersion + 1;
        var scripts = SqliteMigrator.LoadEmbedded()
            .Append(new SqliteMigrator.MigrationScript(nextVersion, $"Migrations/{nextVersion:D4}_broken_fk.sql",
                "INSERT INTO MissingTable (Id) VALUES ('x');"))
            .ToList();

        Assert.Throws<InvalidOperationException>(() => SqliteMigrator.Migrate(connection, scripts));

        Assert.That(ReadScalarLong(connection, "PRAGMA foreign_keys;"), Is.EqualTo(1));
    }

    private static SqliteConnection OpenMemoryConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static int ReadUserVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static List<string> ReadNames(SqliteConnection connection, string type)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = @type AND name NOT LIKE 'sqlite_%' ORDER BY name;";
        command.Parameters.AddWithValue("@type", type);

        var names = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static List<string> ReadRoomColumns(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Rooms);";

        var columns = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static int LatestVersion => SqliteMigrator.LoadEmbedded().Max(script => script.Version);

    private static void MigrateToVersionOne(SqliteConnection connection) =>
        SqliteMigrator.Migrate(connection, SqliteMigrator.LoadEmbedded().Where(script => script.Version <= 1).ToList());

    private static long ReadScalarLong(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static string ReadScalarText(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (string)command.ExecuteScalar()!;
    }

    private static void InsertIctuCampus(SqliteConnection connection) =>
        Execute(connection, """
            INSERT INTO Campuses (Id, Name, ShortName, Latitude, Longitude, IsDefault, SortOrder)
            VALUES ('ICTU', 'Trường Đại học Công nghệ Thông tin và Truyền thông', 'ICTU', 21.5851, 105.8066, 1, 1);
            """);

    private static void InsertVersionOneUser(SqliteConnection connection, string id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Users (Id, Email, NormalizedEmail, FullName, Role, AuthProvider, IsVerified, FailedLoginCount, CreatedAtUtc, UpdatedAtUtc)
            VALUES (@id, @email, @normalizedEmail, 'Test User', 'Student', 'Local', 0, 0, '2026-11-15T08:00:00.0000000Z', '2026-11-15T08:00:00.0000000Z');
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@email", id + "@example.com");
        command.Parameters.AddWithValue("@normalizedEmail", id.ToUpperInvariant() + "@EXAMPLE.COM");
        command.ExecuteNonQuery();
    }
}