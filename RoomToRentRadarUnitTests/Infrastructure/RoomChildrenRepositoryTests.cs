using System.Globalization;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Datastore.Sqlite.Repositories;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;
using RoomToRentRadar.Infrastructure.Adapters.Mocks;

namespace RoomToRentRadarUnitTests.Infrastructure;

/// <summary>
/// T00.9 Phase 3 Integration tests for SqliteRoomRepository and InMemoryRoomRepository child entities persistence:
/// RoomAmenities, RoomSafetyFeatures, and HouseRuleVersions round-trip and concurrency conflict handling.
/// </summary>
[TestFixture]
public class RoomChildrenRepositoryTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
    private static readonly SqliteSettings InMemorySettings = new() { ConnectionString = "Data Source=:memory:" };

    [OneTimeSetUp]
    public void InitializeSqlite() => SQLitePCL.Batteries_V2.Init();

    [Test]
    public async Task SqliteRoomRepository_AddAndGetById_RoundTripsAllChildren()
    {
        using var dbContext = new SqliteDbContext(InMemorySettings);
        using var connection = dbContext.CreateOpenConnection();
        SqliteMigrator.Migrate(connection);
        InsertUser(connection, "landlord-repo-1");

        var repository = new SqliteRoomRepository(dbContext);

        var room = Room.Create(
            landlordId: "landlord-repo-1",
            title: "Phòng trọ đầy đủ tiện nghi và nội quy",
            roomType: RoomType.BoardingRoom,
            monthlyRentVnd: 2_200_000L,
            depositVnd: 2_200_000L,
            areaM2: 22.0,
            addressLine: "Đường Z115",
            ward: "Tân Thịnh",
            district: "TP. Thái Nguyên",
            latitude: 21.5851,
            longitude: 105.8066,
            amenities: new[] { Amenity.Wifi, Amenity.AirConditioner, Amenity.WaterHeater },
            safetyFeatures: new[] { SafetyFeature.FireSafety, SafetyFeature.Cctv },
            utcNow: FixedNow);

        // Admin confirms FireSafety
        room.ConfirmSafetyFeatures(new[] { SafetyFeature.FireSafety }, "admin-1", FixedNow);

        // Add version 1 rules
        room.AddHouseRuleVersion(
            guestPolicy: GuestPolicy.Allowed,
            cookingPolicy: CookingPolicy.Allowed,
            parkingAvailable: true,
            petsAllowed: false,
            smokingAllowed: false,
            createdByUserId: "landlord-repo-1",
            quietHoursStart: "22:00",
            quietHoursEnd: "06:00",
            curfewTime: "23:00",
            parkingRules: "Để xe đúng vị trí vạch kẻ",
            otherRules: "Giữ gìn vệ sinh chung",
            utcNow: FixedNow);

        // Add version 2 rules (updated)
        room.AddHouseRuleVersion(
            guestPolicy: GuestPolicy.DaytimeOnly,
            cookingPolicy: CookingPolicy.SharedKitchenOnly,
            parkingAvailable: true,
            petsAllowed: false,
            smokingAllowed: false,
            createdByUserId: "landlord-repo-1",
            quietHoursStart: "23:00",
            quietHoursEnd: "05:00",
            curfewTime: null,
            parkingRules: "Khóa cổng sau 23h",
            otherRules: "Không làm ồn ban đêm",
            utcNow: FixedNow.AddDays(1));

        await repository.AddAsync(room);

        // Retrieve from datastore
        var loaded = await repository.GetByIdAsync(room.Id);

        Assert.That(loaded, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Id, Is.EqualTo(room.Id));
            Assert.That(loaded.Amenities, Has.Count.EqualTo(3));
            Assert.That(loaded.Amenities, Is.EquivalentTo(new[] { Amenity.Wifi, Amenity.AirConditioner, Amenity.WaterHeater }));

            Assert.That(loaded.SafetyFeatures, Has.Count.EqualTo(2));
            Assert.That(loaded.IsSafetyVerified, Is.True);
            Assert.That(loaded.SafetyScore, Is.EqualTo(1), "Only 1 feature was confirmed, score = 1");
            Assert.That(loaded.SafetyFeatures.First(f => f.Feature == SafetyFeature.FireSafety).IsConfirmed, Is.True);
            Assert.That(loaded.SafetyFeatures.First(f => f.Feature == SafetyFeature.Cctv).IsConfirmed, Is.False);

            Assert.That(loaded.HouseRuleHistory, Has.Count.EqualTo(2));
            Assert.That(loaded.CurrentHouseRules, Is.Not.Null);
            Assert.That(loaded.CurrentHouseRules!.VersionNumber, Is.EqualTo(2));
            Assert.That(loaded.CurrentHouseRules.GuestPolicy, Is.EqualTo(GuestPolicy.DaytimeOnly));
            Assert.That(loaded.CurrentHouseRules.CookingPolicy, Is.EqualTo(CookingPolicy.SharedKitchenOnly));
            Assert.That(loaded.CurrentHouseRules.QuietHoursStart, Is.EqualTo("23:00"));
            Assert.That(loaded.CurrentHouseRules.ParkingRules, Is.EqualTo("Khóa cổng sau 23h"));

            var v1 = loaded.HouseRuleHistory.First(h => h.VersionNumber == 1);
            Assert.That(v1.GuestPolicy, Is.EqualTo(GuestPolicy.Allowed));
            Assert.That(v1.QuietHoursStart, Is.EqualTo("22:00"));
        });
    }

    [Test]
    public async Task SqliteRoomRepository_UpdateAsync_SyncsAmenitiesSafetyAndAppendsNewHouseRuleVersion()
    {
        using var dbContext = new SqliteDbContext(InMemorySettings);
        using var connection = dbContext.CreateOpenConnection();
        SqliteMigrator.Migrate(connection);
        InsertUser(connection, "landlord-repo-1");

        var repository = new SqliteRoomRepository(dbContext);

        var room = Room.Create(
            landlordId: "landlord-repo-1",
            title: "Phòng trọ trước khi cập nhật",
            roomType: RoomType.BoardingRoom,
            monthlyRentVnd: 1_900_000L,
            depositVnd: 1_900_000L,
            areaM2: 18.0,
            addressLine: "Đường Z115",
            ward: "Tân Thịnh",
            district: "TP. Thái Nguyên",
            latitude: 21.5851,
            longitude: 105.8066,
            amenities: new[] { Amenity.Wifi },
            utcNow: FixedNow);

        room.AddHouseRuleVersion(
            guestPolicy: GuestPolicy.Allowed,
            cookingPolicy: CookingPolicy.Allowed,
            parkingAvailable: true,
            petsAllowed: false,
            smokingAllowed: false,
            createdByUserId: "landlord-repo-1",
            utcNow: FixedNow);

        await repository.AddAsync(room);

        // Now modify: update amenities, declare safety features, and add rule v2
        var loaded = await repository.GetByIdAsync(room.Id);
        Assert.That(loaded, Is.Not.Null);

        loaded!.SetAmenities(new[] { Amenity.Wifi, Amenity.AirConditioner, Amenity.Fridge });
        loaded.DeclareSafetyFeatures(new[] { SafetyFeature.Cctv, SafetyFeature.FireSafety });
        loaded.AddHouseRuleVersion(
            guestPolicy: GuestPolicy.NotAllowed,
            cookingPolicy: CookingPolicy.NotAllowed,
            parkingAvailable: false,
            petsAllowed: false,
            smokingAllowed: false,
            createdByUserId: "landlord-repo-1",
            utcNow: FixedNow.AddDays(2));

        await repository.UpdateAsync(loaded);

        // Reload and verify datastore state
        var reloaded = await repository.GetByIdAsync(room.Id);
        Assert.That(reloaded, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(reloaded!.Amenities, Is.EquivalentTo(new[] { Amenity.Wifi, Amenity.AirConditioner, Amenity.Fridge }));
            Assert.That(reloaded.SafetyFeatures, Has.Count.EqualTo(2));
            Assert.That(reloaded.HouseRuleHistory, Has.Count.EqualTo(2));
            Assert.That(reloaded.CurrentHouseRules!.VersionNumber, Is.EqualTo(2));
            Assert.That(reloaded.CurrentHouseRules.GuestPolicy, Is.EqualTo(GuestPolicy.NotAllowed));
        });
    }

    [Test]
    public async Task SqliteRoomRepository_UpdateAsync_ConcurrencyConflict_ThrowsException()
    {
        using var dbContext = new SqliteDbContext(InMemorySettings);
        using var connection = dbContext.CreateOpenConnection();
        SqliteMigrator.Migrate(connection);
        InsertUser(connection, "landlord-repo-1");

        var repository = new SqliteRoomRepository(dbContext);

        var room = Room.Create(
            landlordId: "landlord-repo-1",
            title: "Phòng trọ kiểm tra concurrency",
            roomType: RoomType.BoardingRoom,
            monthlyRentVnd: 2_000_000L,
            depositVnd: 2_000_000L,
            areaM2: 20.0,
            addressLine: "Z115",
            ward: "Tân Thịnh",
            district: "TP. Thái Nguyên",
            latitude: 21.5851,
            longitude: 105.8066,
            utcNow: FixedNow);

        await repository.AddAsync(room);

        // Load 2 instances concurrently
        var instanceA = await repository.GetByIdAsync(room.Id);
        var instanceB = await repository.GetByIdAsync(room.Id);

        // Instance A updates amenities
        instanceA!.SetAmenities(new[] { Amenity.Wifi });
        await repository.UpdateAsync(instanceA);

        // Instance B attempts update with stale version -> must throw ConcurrencyConflictException
        instanceB!.SetAmenities(new[] { Amenity.AirConditioner });
        Assert.ThrowsAsync<ConcurrencyConflictException>(async () => await repository.UpdateAsync(instanceB));
    }

    [Test]
    public async Task InMemoryRoomRepository_ParityWithSqlite_PreservesAllChildren()
    {
        var repository = new InMemoryRoomRepository();

        var room = Room.Create(
            landlordId: "landlord-mem",
            title: "Phòng trọ InMemory Test",
            roomType: RoomType.BoardingRoom,
            monthlyRentVnd: 2_000_000L,
            depositVnd: 2_000_000L,
            areaM2: 20.0,
            addressLine: "Z115",
            ward: "Tân Thịnh",
            district: "TP. Thái Nguyên",
            latitude: 21.5851,
            longitude: 105.8066,
            amenities: new[] { Amenity.Wifi, Amenity.Balcony },
            safetyFeatures: new[] { SafetyFeature.Cctv },
            utcNow: FixedNow);

        room.AddHouseRuleVersion(
            guestPolicy: GuestPolicy.Allowed,
            cookingPolicy: CookingPolicy.Allowed,
            parkingAvailable: true,
            petsAllowed: true,
            smokingAllowed: false,
            createdByUserId: "landlord-mem",
            utcNow: FixedNow);

        await repository.AddAsync(room);

        var loaded = await repository.GetByIdAsync(room.Id);
        Assert.That(loaded, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Amenities, Is.EquivalentTo(new[] { Amenity.Wifi, Amenity.Balcony }));
            Assert.That(loaded.SafetyFeatures, Has.Count.EqualTo(1));
            Assert.That(loaded.HouseRuleHistory, Has.Count.EqualTo(1));
            Assert.That(loaded.CurrentHouseRules!.PetsAllowed, Is.True);
        });

        // Test update and deep copy isolation
        loaded!.SetAmenities(new[] { Amenity.Wifi, Amenity.Elevator });
        await repository.UpdateAsync(loaded);

        var reloaded = await repository.GetByIdAsync(room.Id);
        Assert.That(reloaded!.Amenities, Is.EquivalentTo(new[] { Amenity.Wifi, Amenity.Elevator }));
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
}
