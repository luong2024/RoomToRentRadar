using System.Globalization;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Context;

namespace RoomToRentRadarUnitTests.Infrastructure;

/// <summary>
/// T00.9 / Migration 0003 verification: tests schema creation, indexes, foreign keys,
/// and cascade delete for RoomAmenities, RoomSafetyFeatures, and HouseRuleVersions (<c>docs/erd.md</c> §4.5..4.7).
/// </summary>
[TestFixture]
public class RoomChildrenMigrationTests
{
    [OneTimeSetUp]
    public void InitializeSqlite() => SQLitePCL.Batteries_V2.Init();

    [Test]
    public void Migrate_CreatesRoomChildrenTablesAndIndexes()
    {
        using var connection = OpenMemoryConnection();
        var applied = SqliteMigrator.Migrate(connection);

        Assert.Multiple(() =>
        {
            Assert.That(applied, Does.Contain(3));
            Assert.That(ReadUserVersion(connection), Is.EqualTo(3));

            var tables = ReadNames(connection, "table");
            Assert.That(tables, Does.Contain("RoomAmenities"));
            Assert.That(tables, Does.Contain("RoomSafetyFeatures"));
            Assert.That(tables, Does.Contain("HouseRuleVersions"));

            var indexes = ReadNames(connection, "index");
            Assert.That(indexes, Does.Contain("IX_HouseRuleVersions_RoomId"));

            var amenityColumns = ReadTableColumns(connection, "RoomAmenities");
            Assert.That(amenityColumns, Is.EquivalentTo(new[] { "RoomId", "AmenityCode" }));

            var safetyColumns = ReadTableColumns(connection, "RoomSafetyFeatures");
            Assert.That(safetyColumns, Is.EquivalentTo(new[] { "RoomId", "FeatureCode", "IsConfirmed" }));

            var houseRuleColumns = ReadTableColumns(connection, "HouseRuleVersions");
            Assert.That(houseRuleColumns, Is.EquivalentTo(new[]
            {
                "Id", "RoomId", "VersionNumber", "QuietHoursStart", "QuietHoursEnd",
                "CurfewTime", "GuestPolicy", "GuestPolicyNote", "CookingPolicy",
                "ParkingAvailable", "ParkingRules", "PetsAllowed", "SmokingAllowed",
                "OtherRules", "CreatedByUserId", "CreatedAtUtc"
            }));

            // PRAGMA foreign_key_check must be completely clean
            Assert.That(ReadForeignKeyViolations(connection), Is.Empty);
        });
    }

    [Test]
    public void Migrate_IncrementalFromVersionTwo_AppliesOnlyVersionThree()
    {
        using var connection = OpenMemoryConnection();
        // Migrate to version 2 only
        var v2Scripts = SqliteMigrator.LoadEmbedded().Where(s => s.Version <= 2).ToList();
        var v2Applied = SqliteMigrator.Migrate(connection, v2Scripts);
        Assert.That(v2Applied, Is.EqualTo(new[] { 1, 2 }));
        Assert.That(ReadUserVersion(connection), Is.EqualTo(2));

        // Now run full migration (script 3 is applied incrementally)
        var applied = SqliteMigrator.Migrate(connection);

        Assert.Multiple(() =>
        {
            Assert.That(applied, Is.EqualTo(new[] { 3 }));
            Assert.That(ReadUserVersion(connection), Is.EqualTo(3));
            Assert.That(ReadNames(connection, "table"), Does.Contain("RoomAmenities"));
            Assert.That(ReadNames(connection, "table"), Does.Contain("RoomSafetyFeatures"));
            Assert.That(ReadNames(connection, "table"), Does.Contain("HouseRuleVersions"));
        });
    }

    [Test]
    public void CascadeDelete_DeletingRoom_DeletesAmenitiesSafetyFeaturesAndHouseRules()
    {
        using var connection = OpenMemoryConnection();
        SqliteMigrator.Migrate(connection);

        InsertUser(connection, "landlord-1");
        InsertRoom(connection, "room-1", "landlord-1");

        Execute(connection, """
            INSERT INTO RoomAmenities (RoomId, AmenityCode) VALUES ('room-1', 'Wifi');
            INSERT INTO RoomAmenities (RoomId, AmenityCode) VALUES ('room-1', 'AirConditioner');
            INSERT INTO RoomSafetyFeatures (RoomId, FeatureCode, IsConfirmed) VALUES ('room-1', 'FireSafety', 1);
            INSERT INTO HouseRuleVersions (
                Id, RoomId, VersionNumber, GuestPolicy, CookingPolicy,
                ParkingAvailable, PetsAllowed, SmokingAllowed, CreatedByUserId, CreatedAtUtc
            ) VALUES (
                'hrv-1', 'room-1', 1, 'Allowed', 'Allowed', 1, 0, 0, 'landlord-1', '2026-11-18T12:00:00.0000000Z'
            );
            """);

        Assert.Multiple(() =>
        {
            Assert.That(ReadScalarLong(connection, "SELECT COUNT(1) FROM RoomAmenities WHERE RoomId = 'room-1';"), Is.EqualTo(2));
            Assert.That(ReadScalarLong(connection, "SELECT COUNT(1) FROM RoomSafetyFeatures WHERE RoomId = 'room-1';"), Is.EqualTo(1));
            Assert.That(ReadScalarLong(connection, "SELECT COUNT(1) FROM HouseRuleVersions WHERE RoomId = 'room-1';"), Is.EqualTo(1));
        });

        // Delete the parent room
        Execute(connection, "DELETE FROM Rooms WHERE Id = 'room-1';");

        // Verify CASCADE deleted all child rows
        Assert.Multiple(() =>
        {
            Assert.That(ReadScalarLong(connection, "SELECT COUNT(1) FROM RoomAmenities WHERE RoomId = 'room-1';"), Is.EqualTo(0));
            Assert.That(ReadScalarLong(connection, "SELECT COUNT(1) FROM RoomSafetyFeatures WHERE RoomId = 'room-1';"), Is.EqualTo(0));
            Assert.That(ReadScalarLong(connection, "SELECT COUNT(1) FROM HouseRuleVersions WHERE RoomId = 'room-1';"), Is.EqualTo(0));
            Assert.That(ReadForeignKeyViolations(connection), Is.Empty);
        });
    }

    [Test]
    public void HouseRuleVersions_UniqueConstraint_PreventsDuplicateVersionNumberForSameRoom()
    {
        using var connection = OpenMemoryConnection();
        SqliteMigrator.Migrate(connection);

        InsertUser(connection, "landlord-1");
        InsertRoom(connection, "room-1", "landlord-1");

        Execute(connection, """
            INSERT INTO HouseRuleVersions (
                Id, RoomId, VersionNumber, GuestPolicy, CookingPolicy,
                ParkingAvailable, PetsAllowed, SmokingAllowed, CreatedByUserId, CreatedAtUtc
            ) VALUES (
                'hrv-1', 'room-1', 1, 'Allowed', 'Allowed', 1, 0, 0, 'landlord-1', '2026-11-18T12:00:00.0000000Z'
            );
            """);

        // Attempting to insert duplicate version number 1 for room-1 must fail
        var ex = Assert.Throws<SqliteException>(() => Execute(connection, """
            INSERT INTO HouseRuleVersions (
                Id, RoomId, VersionNumber, GuestPolicy, CookingPolicy,
                ParkingAvailable, PetsAllowed, SmokingAllowed, CreatedByUserId, CreatedAtUtc
            ) VALUES (
                'hrv-2', 'room-1', 1, 'DaytimeOnly', 'NotAllowed', 0, 0, 0, 'landlord-1', '2026-11-18T13:00:00.0000000Z'
            );
            """));

        Assert.That(ex!.SqliteExtendedErrorCode, Is.EqualTo(2067), "SQLITE_CONSTRAINT_UNIQUE on (RoomId, VersionNumber).");
    }

    [Test]
    public void ForeignKeys_InsertingChildWithInvalidRoomId_ThrowsForeignKeyException()
    {
        using var connection = OpenMemoryConnection();
        SqliteMigrator.Migrate(connection);

        var ex = Assert.Throws<SqliteException>(() => Execute(connection, """
            INSERT INTO RoomAmenities (RoomId, AmenityCode) VALUES ('non-existent-room', 'Wifi');
            """));

        Assert.That(ex!.SqliteExtendedErrorCode, Is.EqualTo(787), "SQLITE_CONSTRAINT_FOREIGNKEY.");
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

    private static long ReadScalarLong(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
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

    private static List<string> ReadTableColumns(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info('{tableName}');";

        var columns = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static List<string> ReadForeignKeyViolations(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";

        var violations = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            violations.Add($"{reader.GetString(0)} rowid={reader.GetInt64(1)} references {reader.GetString(2)}");
        }

        return violations;
    }

    private static void InsertUser(SqliteConnection connection, string userId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Users (Id, Email, NormalizedEmail, FullName, Role, AuthProvider, IsVerified, FailedLoginCount, CreatedAtUtc, UpdatedAtUtc, SelectedCampusId, PreferredLanguage, Version)
            VALUES (@id, @email, @normalizedEmail, 'Landlord Test', 'Landlord', 'Local', 1, 0, '2026-11-18T00:00:00Z', '2026-11-18T00:00:00Z', 'ICTU', 'vi', 1);
            """;
        command.Parameters.AddWithValue("@id", userId);
        command.Parameters.AddWithValue("@email", $"{userId}@example.com");
        command.Parameters.AddWithValue("@normalizedEmail", $"{userId.ToUpperInvariant()}@EXAMPLE.COM");
        command.ExecuteNonQuery();
    }

    private static void InsertRoom(SqliteConnection connection, string roomId, string landlordId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Rooms (
                Id, LandlordId, Title, Description, RoomType, MonthlyRentVnd, DepositVnd, AreaM2,
                AddressLine, Ward, District, Latitude, Longitude, Status, NeedsReReview, IsSafetyVerified,
                LastUpdatedAtUtc, CreatedAtUtc, Version
            ) VALUES (
                @id, @landlordId, 'Phòng trọ cao cấp gần ICTU', 'Mô tả phòng', 'BoardingRoom', 1500000, 1500000, 20.0,
                'Đường Z115', 'Tân Thịnh', 'TP. Thái Nguyên', 21.5851, 105.8066, 'Available', 0, 0,
                '2026-11-18T00:00:00Z', '2026-11-18T00:00:00Z', 1
            );
            """;
        command.Parameters.AddWithValue("@id", roomId);
        command.Parameters.AddWithValue("@landlordId", landlordId);
        command.ExecuteNonQuery();
    }
}
