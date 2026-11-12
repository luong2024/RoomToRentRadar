using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Datastore.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Datastore.Sqlite.Repositories;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Infrastructure;

[TestFixture]
public class SqliteRoomRepositoryTests
{
    private static readonly SqliteSettings InMemorySettings = new() { ConnectionString = "Data Source=:memory:" };

    [Test]
    public async Task GetAllAsync_ReturnsEightSeededRooms_WithSixVerifiedAndAvailable()
    {
        using var dbContext = new SqliteDbContext(InMemorySettings);
        IRoomRepository repo = new SqliteRoomRepository(dbContext);

        var rooms = await repo.GetAllAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rooms, Has.Count.EqualTo(8));
            Assert.That(rooms.Where(r => r.IsVerified && r.IsAvailable), Has.Exactly(6).Items);
            Assert.That(rooms, Has.Some.Matches<Room>(r => r.Id == "seed-chua-xac-minh" && !r.IsVerified));
            Assert.That(rooms, Has.Some.Matches<Room>(r => r.Id == "seed-da-thue" && !r.IsAvailable));
        });
    }

    [Test]
    public async Task GetAllAsync_SeededRoomsMatchReferenceData()
    {
        using var dbContext = new SqliteDbContext(InMemorySettings);
        IRoomRepository repo = new SqliteRoomRepository(dbContext);

        var rooms = await repo.GetAllAsync();
        var z115 = rooms.Single(r => r.Id == "seed-z115");
        var dongHy = rooms.Single(r => r.Id == "seed-dong-hy");

        Assert.Multiple(() =>
        {
            Assert.That(z115.Title, Is.EqualTo("Phòng trọ Sinh viên Z115"));
            Assert.That(z115.Description, Is.EqualTo("Phòng sạch sẽ, gần trường ICTU"));
            Assert.That(z115.Price, Is.EqualTo(1_500_000m));
            Assert.That(z115.Area, Is.EqualTo(18d));
            Assert.That(z115.Address, Is.EqualTo("Z115, phường Tân Thịnh, Thái Nguyên"));
            Assert.That(z115.Latitude, Is.EqualTo(21.5875));
            Assert.That(z115.Longitude, Is.EqualTo(105.8080));
            Assert.That(z115.LandlordId, Is.EqualTo("seed-landlord"));
            Assert.That(z115.CreatedAtUtc, Is.EqualTo(new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc)));
            Assert.That(dongHy.Price, Is.EqualTo(900_000m));
            Assert.That(dongHy.CreatedAtUtc, Is.EqualTo(new DateTime(2026, 11, 1, 8, 5, 0, DateTimeKind.Utc)));
        });
    }

    [Test]
    public async Task GetAllAsync_CalledTwice_DoesNotSeedDuplicates()
    {
        using var dbContext = new SqliteDbContext(InMemorySettings);
        IRoomRepository repo = new SqliteRoomRepository(dbContext);

        await repo.GetAllAsync();
        var rooms = await repo.GetAllAsync();

        Assert.That(rooms, Has.Count.EqualTo(8));
    }

    [Test]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsRoom()
    {
        using var dbContext = new SqliteDbContext(InMemorySettings);
        IRoomRepository repo = new SqliteRoomRepository(dbContext);

        var room = Room.Create(
            title: "Phòng mới SQLite",
            description: "Mô tả phòng mới",
            price: 1_850_000m,
            area: 20.5,
            address: "Tân Thịnh, Thái Nguyên",
            latitude: 21.5851,
            longitude: 105.8066,
            landlordId: "landlord-1");

        await repo.AddAsync(room);
        var found = await repo.GetByIdAsync(room.Id);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.Not.Null);
            Assert.That(found!.Id, Is.EqualTo(room.Id));
            Assert.That(found.Title, Is.EqualTo(room.Title));
            Assert.That(found.Description, Is.EqualTo(room.Description));
            Assert.That(found.Price, Is.EqualTo(1_850_000m));
            Assert.That(found.Area, Is.EqualTo(20.5));
            Assert.That(found.Address, Is.EqualTo(room.Address));
            Assert.That(found.Latitude, Is.EqualTo(21.5851));
            Assert.That(found.Longitude, Is.EqualTo(105.8066));
            Assert.That(found.LandlordId, Is.EqualTo("landlord-1"));
            Assert.That(found.IsAvailable, Is.True);
            Assert.That(found.IsVerified, Is.True);
        });
    }

    [Test]
    public void AddAsync_NullRoom_ThrowsArgumentNullException()
    {
        using var dbContext = new SqliteDbContext(InMemorySettings);
        IRoomRepository repo = new SqliteRoomRepository(dbContext);

        Assert.ThrowsAsync<ArgumentNullException>(async () => await repo.AddAsync(null!));
    }

    [Test]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        using var dbContext = new SqliteDbContext(InMemorySettings);
        IRoomRepository repo = new SqliteRoomRepository(dbContext);

        var found = await repo.GetByIdAsync("does-not-exist");

        Assert.That(found, Is.Null);
    }

    [Test]
    public async Task GetSimilarRoomsAsync_FiltersAvailableAndWithinDistanceAndPriceTolerance()
    {
        using var dbContext = new SqliteDbContext(InMemorySettings);
        var repo = new SqliteRoomRepository(dbContext);

        // Target: 1.5M, lat: 21.5875, lng: 105.8080 (seed-z115)
        var similar = await repo.GetSimilarRoomsAsync(
            targetRoomId: "seed-z115",
            targetMonthlyRentVnd: 1_500_000m,
            targetLatitude: 21.5875,
            targetLongitude: 105.8080,
            roomType: RoomType.BoardingRoom,
            limit: 5);

        Assert.Multiple(() =>
        {
            Assert.That(similar, Is.Not.Null);
            // Must not include target room itself
            Assert.That(similar.Any(r => r.Id == "seed-z115"), Is.False);
            // All must be Available
            Assert.That(similar.All(r => r.IsAvailable), Is.True);
            // All within 2 km
            Assert.That(similar.All(r => r.DistanceKm <= 2.0), Is.True);
            // All within price tolerance +/- 20% (1.2M - 1.8M)
            Assert.That(similar.All(r => r.MonthlyRentVnd >= 1_200_000m && r.MonthlyRentVnd <= 1_800_000m), Is.True);
        });
    }

    [Test]
    public void DependencyInjection_AddSqliteDatastore_RegistersSqliteRoomRepository()
    {
        var services = new ServiceCollection();
        services.AddSqliteDatastore(InMemorySettings);

        var repoDescriptor = services.Single(d => d.ServiceType == typeof(IRoomRepository));
        var searchDescriptor = services.Single(d => d.ServiceType == typeof(IRoomSearchReadModel));

        Assert.That(repoDescriptor.ImplementationType, Is.EqualTo(typeof(SqliteRoomRepository)));
        Assert.That(searchDescriptor.ImplementationType, Is.EqualTo(typeof(SqliteRoomRepository)));
    }
}
