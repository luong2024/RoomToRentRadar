using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Datastore.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Datastore.Sqlite.Repositories;
using RoomToRentRadar.Domain.Entities;
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

        var room = new Room
        {
            Id = "room-new",
            Title = "Phòng mới SQLite",
            Description = "Mô tả phòng mới",
            Price = 1_850_000m,
            Area = 20.5,
            Address = "Tân Thịnh, Thái Nguyên",
            Latitude = 21.5851,
            Longitude = 105.8066,
            LandlordId = "landlord-1",
            IsAvailable = false,
            IsVerified = true,
            CreatedAtUtc = new DateTime(2026, 11, 2, 9, 30, 0, DateTimeKind.Utc)
        };

        await repo.AddAsync(room);
        var found = await repo.GetByIdAsync("room-new");

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
            Assert.That(found.IsAvailable, Is.False);
            Assert.That(found.IsVerified, Is.True);
            Assert.That(found.CreatedAtUtc, Is.EqualTo(room.CreatedAtUtc));
        });
    }

    [Test]
    public async Task AddAsync_NullRoom_ThrowsArgumentNullException()
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
    public async Task GetAllAsync_ReturnsListCopy_SoExternalMutationDoesNotAffectStore()
    {
        using var dbContext = new SqliteDbContext(InMemorySettings);
        IRoomRepository repo = new SqliteRoomRepository(dbContext);

        var snapshot = await repo.GetAllAsync();
        if (snapshot is ICollection<Room> mutable)
        {
            mutable.Clear();
        }

        var after = await repo.GetAllAsync();
        Assert.That(after, Has.Count.EqualTo(8));
    }

    [Test]
    public void DependencyInjection_AddSqliteDatastore_RegistersSqliteRoomRepository()
    {
        var services = new ServiceCollection();
        services.AddSqliteDatastore(InMemorySettings);

        var descriptor = services.Single(d => d.ServiceType == typeof(IRoomRepository));

        Assert.That(descriptor.ImplementationType, Is.EqualTo(typeof(SqliteRoomRepository)));
    }
}
