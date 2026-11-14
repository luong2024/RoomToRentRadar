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
        "AuditLogs", "Campuses", "Rooms", "UserRefreshTokens", "Users"
    ];

    private static readonly string[] BaselineIndexNames =
    [
        "IX_AuditLogs_Action", "IX_AuditLogs_TimestampUtc", "IX_AuditLogs_UserId",
        "IX_Rooms_LandlordId", "IX_Rooms_NeedsReReview", "IX_Rooms_Status_Lat_Lng",
        "IX_Rooms_Status_PublishedAtUtc", "IX_Rooms_Status_Rent",
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
            Assert.That(applied, Is.EqualTo(new[] { 1 }));
            Assert.That(ReadNames(connection, "table"), Is.EquivalentTo(BaselineTableNames));
            Assert.That(ReadNames(connection, "index"), Is.EquivalentTo(BaselineIndexNames));

            var columns = ReadRoomColumns(connection);
            Assert.That(columns, Has.Count.EqualTo(32));
            Assert.That(columns, Is.EquivalentTo(RoomColumnNames));
            Assert.That(columns, Does.Not.Contain("ConcurrencyToken"),
                "ConcurrencyToken là Version.ToString() (ERD §1) — không được lưu thành cột riêng.");
            Assert.That(ReadUserVersion(connection), Is.EqualTo(1));
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
            Assert.That(ReadUserVersion(connection), Is.EqualTo(1));
        });
    }

    [Test]
    public void Migrate_NewerScriptAvailable_AppliesOnlyNewerScript()
    {
        using var connection = OpenMemoryConnection();
        SqliteMigrator.Migrate(connection);
        var scripts = SqliteMigrator.LoadEmbedded()
            .Append(new SqliteMigrator.MigrationScript(2, "Migrations/0002_fake.sql",
                "CREATE TABLE FakeTwo (Id TEXT PRIMARY KEY NOT NULL);"))
            .ToList();

        var applied = SqliteMigrator.Migrate(connection, scripts);

        Assert.Multiple(() =>
        {
            Assert.That(applied, Is.EqualTo(new[] { 2 }));
            Assert.That(ReadNames(connection, "table"), Does.Contain("FakeTwo"));
            Assert.That(ReadUserVersion(connection), Is.EqualTo(2));
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
        var scripts = SqliteMigrator.LoadEmbedded()
            .Append(new SqliteMigrator.MigrationScript(2, "Migrations/0002_broken.sql",
                "CREATE TABLE ShouldNotExist (Id TEXT PRIMARY KEY NOT NULL); INSERT INTO MissingTable (Id) VALUES ('x');"))
            .ToList();

        var exception = Assert.Throws<InvalidOperationException>(() => SqliteMigrator.Migrate(connection, scripts));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("0002"));
            Assert.That(ReadUserVersion(connection), Is.EqualTo(1));
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
}