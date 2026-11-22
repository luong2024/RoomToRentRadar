using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Datastore.Sqlite.Repositories;
using RoomToRentRadar.Datastore.Sqlite.Seed;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Infrastructure;

[TestFixture]
public class DemoDataSeedTests
{
    [Test]
    public void ExtendedFlagOff_SeedDemoRoomsOn_KeepsExactlyEightCoreRoomsAndNoChildren()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings
        {
            ConnectionString = "Data Source=:memory:",
            SeedDemoRooms = true,
            SeedExtendedDemoData = false
        });
        using var conn = dbContext.CreateOpenConnection();

        Assert.Multiple(() =>
        {
            Assert.That(CountTable(conn, "Rooms"), Is.EqualTo(8));
            Assert.That(CountTable(conn, "RoomAmenities"), Is.Zero);
            Assert.That(CountTable(conn, "RoomSafetyFeatures"), Is.Zero);
            Assert.That(CountTable(conn, "HouseRuleVersions"), Is.Zero);
        });
    }

    [Test]
    public void ExtendedFlagOn_WithoutSeedDemoRooms_SeedsNothing()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings
        {
            ConnectionString = "Data Source=:memory:",
            SeedDemoRooms = false,
            SeedExtendedDemoData = true
        });
        using var conn = dbContext.CreateOpenConnection();

        Assert.Multiple(() =>
        {
            Assert.That(CountTable(conn, "Rooms"), Is.Zero);
            Assert.That(CountTable(conn, "RoomAmenities"), Is.Zero);
            Assert.That(CountTable(conn, "RoomSafetyFeatures"), Is.Zero);
            Assert.That(CountTable(conn, "HouseRuleVersions"), Is.Zero);
            Assert.That(CountTable(conn, "Users"), Is.Zero);
        });
    }

    [Test]
    public void ExtendedFlagOn_Seeds30RoomsWithStatusMix_22Available3Rented3Pending2Rejected()
    {
        using var dbContext = CreateExtendedContext();
        using var conn = dbContext.CreateOpenConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Status, COUNT(1) FROM Rooms GROUP BY Status;";
        var byStatus = new Dictionary<string, long>(StringComparer.Ordinal);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            byStatus[reader.GetString(0)] = reader.GetInt64(1);
        }

        Assert.Multiple(() =>
        {
            Assert.That(CountTable(conn, "Rooms"), Is.EqualTo(30));
            Assert.That(byStatus.GetValueOrDefault("Available"), Is.EqualTo(22));
            Assert.That(byStatus.GetValueOrDefault("Rented"), Is.EqualTo(3));
            Assert.That(byStatus.GetValueOrDefault("PendingReview"), Is.EqualTo(3));
            Assert.That(byStatus.GetValueOrDefault("Rejected"), Is.EqualTo(2));
        });
    }

    [Test]
    public void ExtendedFlagOn_CoversFourRoomTypes_AndRentBetween900kAnd4M()
    {
        using var dbContext = CreateExtendedContext();
        using var conn = dbContext.CreateOpenConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT RoomType FROM Rooms;";
        var roomTypes = new HashSet<string>(StringComparer.Ordinal);
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                roomTypes.Add(reader.GetString(0));
            }
        }

        using var rentCmd = conn.CreateCommand();
        rentCmd.CommandText = "SELECT MIN(MonthlyRentVnd), MAX(MonthlyRentVnd) FROM Rooms;";
        using var rentReader = rentCmd.ExecuteReader();
        rentReader.Read();
        var minRent = rentReader.GetInt64(0);
        var maxRent = rentReader.GetInt64(1);

        Assert.Multiple(() =>
        {
            Assert.That(roomTypes, Is.EquivalentTo(new[]
            {
                nameof(RoomType.BoardingRoom),
                nameof(RoomType.MiniApartment),
                nameof(RoomType.WholeHouse),
                nameof(RoomType.Dormitory)
            }));
            Assert.That(minRent, Is.EqualTo(900_000L));
            Assert.That(maxRent, Is.EqualTo(4_000_000L));
        });
    }

    [Test]
    public void ExtendedFlagOn_AtLeastFourRoomsWithinTwoKmOfEachOfIctuTnutTnueTump()
    {
        var campuses = CampusSeedData.CreateCampuses().ToDictionary(c => c.Id, StringComparer.Ordinal);
        var targetCampuses = new[] { "ICTU", "TNUT", "TNUE", "TUMP" };

        using var dbContext = CreateExtendedContext();
        using var conn = dbContext.CreateOpenConnection();

        var availableRooms = new List<(string Id, double Lat, double Lng)>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Latitude, Longitude FROM Rooms WHERE Status = 'Available';";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            availableRooms.Add((reader.GetString(0), reader.GetDouble(1), reader.GetDouble(2)));
        }

        Assert.Multiple(() =>
        {
            foreach (var campusId in targetCampuses)
            {
                var campus = campuses[campusId];
                var nearbyCount = availableRooms.Count(r =>
                    HaversineDistanceCalculator.CalculateDistanceKm(campus.Latitude, campus.Longitude, r.Lat, r.Lng) <= 2.0);
                Assert.That(nearbyCount, Is.GreaterThanOrEqualTo(4), $"Expected >= 4 Available rooms within 2 km of {campusId}.");
            }
        });
    }

    [Test]
    public async Task ExtendedFlagOn_EveryRoom_HasAmenitiesSafetyFeaturesAndOneCurrentHouseRules()
    {
        using var dbContext = CreateExtendedContext();
        IRoomRepository repo = new SqliteRoomRepository(dbContext);
        var allEntries = DemoRoomCatalog.CreateAllEntries();

        foreach (var entry in allEntries)
        {
            var loaded = await repo.GetByIdAsync(entry.Room.Id);
            Assert.That(loaded, Is.Not.Null, $"Room '{entry.Room.Id}' should exist.");
            Assert.Multiple(() =>
            {
                Assert.That(loaded!.Amenities, Is.Not.Empty, entry.Room.Id);
                Assert.That(loaded.SafetyFeatures, Is.Not.Empty, entry.Room.Id);
                Assert.That(loaded.CurrentHouseRules, Is.Not.Null, entry.Room.Id);
            });
        }
    }

    [Test]
    public async Task ExtendedFlagOn_Demo02_HasTwoHouseRuleVersions_CurrentIsVersionTwo()
    {
        using var dbContext = CreateExtendedContext();
        using var conn = dbContext.CreateOpenConnection();

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(1) FROM HouseRuleVersions WHERE RoomId = 'demo-02';";
        var versionCount = Convert.ToInt64(countCmd.ExecuteScalar());

        IRoomRepository repo = new SqliteRoomRepository(dbContext);
        var demo02 = await repo.GetByIdAsync("demo-02");

        Assert.Multiple(() =>
        {
            Assert.That(versionCount, Is.EqualTo(2));
            Assert.That(demo02, Is.Not.Null);
            Assert.That(demo02!.CurrentHouseRules, Is.Not.Null);
            Assert.That(demo02.CurrentHouseRules!.VersionNumber, Is.EqualTo(2));
            Assert.That(demo02.CurrentHouseRules.CurfewTime, Is.EqualTo("23:30"));
        });
    }

    [Test]
    public async Task ExtendedFlagOn_SafetyFeaturesConfirmedExactlyForSafetyVerifiedRooms()
    {
        using var dbContext = CreateExtendedContext();
        IRoomRepository repo = new SqliteRoomRepository(dbContext);

        foreach (var entry in DemoRoomCatalog.CreateAllEntries())
        {
            var loaded = (await repo.GetByIdAsync(entry.Room.Id))!;
            Assert.That(
                loaded.SafetyFeatures.All(sf => sf.IsConfirmed == loaded.IsSafetyVerified),
                Is.True,
                $"Room '{loaded.Id}' safety feature confirmation must match IsSafetyVerified={loaded.IsSafetyVerified}.");
        }
    }

    [Test]
    public void ExtendedFlagOn_NoRoomHasPhotos()
    {
        using var dbContext = CreateExtendedContext();
        using var conn = dbContext.CreateOpenConnection();

        Assert.That(CountTable(conn, "RoomPhotos"), Is.Zero);
    }

    [Test]
    public void Seed_RunTwiceOnFileDb_IsIdempotent_AndDoesNotResurrectDeletedAmenities()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rtr_demo_seed_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "demo_idempotent.db");
        var connStr = $"Data Source={dbPath}";

        try
        {
            long initialRooms;
            long initialAmenities;
            long initialSafety;
            long initialRules;
            long expectedAmenitiesAfterDelete;

            using (var firstContext = new SqliteDbContext(new SqliteSettings
            {
                ConnectionString = connStr,
                SeedDemoRooms = true,
                SeedExtendedDemoData = true
            }))
            {
                using var conn = firstContext.CreateOpenConnection();
                initialRooms = CountTable(conn, "Rooms");
                initialAmenities = CountTable(conn, "RoomAmenities");
                initialSafety = CountTable(conn, "RoomSafetyFeatures");
                initialRules = CountTable(conn, "HouseRuleVersions");

                using var deleteCmd = conn.CreateCommand();
                deleteCmd.CommandText = "DELETE FROM RoomAmenities WHERE RoomId = 'demo-01';";
                var deletedCount = deleteCmd.ExecuteNonQuery();
                Assert.That(deletedCount, Is.GreaterThan(0));
                expectedAmenitiesAfterDelete = initialAmenities - deletedCount;
            }

            using (var secondContext = new SqliteDbContext(new SqliteSettings
            {
                ConnectionString = connStr,
                SeedDemoRooms = true,
                SeedExtendedDemoData = true
            }))
            {
                using var conn = secondContext.CreateOpenConnection();

                using var demo01AmenityCmd = conn.CreateCommand();
                demo01AmenityCmd.CommandText = "SELECT COUNT(1) FROM RoomAmenities WHERE RoomId = 'demo-01';";
                var demo01Amenities = Convert.ToInt64(demo01AmenityCmd.ExecuteScalar());

                Assert.Multiple(() =>
                {
                    Assert.That(CountTable(conn, "Rooms"), Is.EqualTo(initialRooms));
                    Assert.That(CountTable(conn, "RoomAmenities"), Is.EqualTo(expectedAmenitiesAfterDelete));
                    Assert.That(demo01Amenities, Is.Zero, "Deleted amenities must not be resurrected.");
                    Assert.That(CountTable(conn, "RoomSafetyFeatures"), Is.EqualTo(initialSafety));
                    Assert.That(CountTable(conn, "HouseRuleVersions"), Is.EqualTo(initialRules));
                });
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Test]
    public void AllSeededRoomsSatisfyDomainInvariants()
    {
        var entries = DemoRoomCatalog.CreateAllEntries();
        var ids = entries.Select(e => e.Room.Id).ToList();
        var titles = entries.Select(e => e.Room.Title).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(entries, Has.Count.EqualTo(30));
            Assert.That(ids, Is.Unique);
            Assert.That(titles, Is.Unique);
            Assert.That(entries.All(e => e.Room.Amenities.Count > 0), Is.True);
            Assert.That(entries.All(e => e.Room.SafetyFeatures.Count > 0), Is.True);
            Assert.That(entries.All(e => e.Room.CurrentHouseRules is not null), Is.True);
        });
    }

    [Test]
    public void ProductionDefaultSettings_NoDemoRoomsNoDemoAccounts()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings
        {
            ConnectionString = "Data Source=:memory:"
        });
        using var conn = dbContext.CreateOpenConnection();

        Assert.Multiple(() =>
        {
            Assert.That(CountTable(conn, "Rooms"), Is.Zero);
            Assert.That(CountTable(conn, "Users"), Is.Zero);
            Assert.That(CountTable(conn, "Campuses"), Is.EqualTo(8));
        });
    }

    private static SqliteDbContext CreateExtendedContext() =>
        new(new SqliteSettings
        {
            ConnectionString = "Data Source=:memory:",
            SeedDemoRooms = true,
            SeedExtendedDemoData = true
        });

    private static long CountTable(SqliteConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(1) FROM {tableName};";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }
}
