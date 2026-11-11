using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Datastore.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Datastore.Sqlite.Repositories;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Infrastructure;
using RoomToRentRadar.Infrastructure.Adapters.Mocks;
using RoomToRentRadar.Infrastructure.Adapters.Storage;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Infrastructure;

[TestFixture]
public class SqliteRoomPhotoRepositoryTests
{
    private static readonly DateTime BaseUtc = new(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc);

    private static RoomPhoto CreatePhoto(string roomId, string id, DateTime uploadedAtUtc, string fileName = "photo.jpg")
    {
        return new RoomPhoto
        {
            Id = id,
            RoomId = roomId,
            FileName = fileName,
            Url = $"/uploads/rooms/{roomId}/{fileName}",
            ContentType = "image/jpeg",
            SizeBytes = 1234,
            UploadedByUserId = "landlord-1",
            UploadedAtUtc = uploadedAtUtc
        };
    }

    private static string CreateTempUploadsPath()
    {
        return Path.Combine(Path.GetTempPath(), "rtrr-photos", Guid.NewGuid().ToString("N"));
    }

    [Test]
    public async Task ListByRoomAsync_ReturnsPhotosOrderedByUploadedAtUtcAscending()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IRoomPhotoRepository repository = new SqliteRoomPhotoRepository(dbContext);

        await repository.AddAsync(CreatePhoto("room-1", "photo-2", BaseUtc.AddMinutes(2)));
        await repository.AddAsync(CreatePhoto("room-1", "photo-0", BaseUtc));
        await repository.AddAsync(CreatePhoto("room-1", "photo-1", BaseUtc.AddMinutes(1)));

        var photos = await repository.ListByRoomAsync("room-1");

        Assert.Multiple(() =>
        {
            Assert.That(photos.Count, Is.EqualTo(3));
            Assert.That(photos.Select(p => p.Id), Is.EqualTo(new[] { "photo-0", "photo-1", "photo-2" }));
            Assert.That(photos[0].RoomId, Is.EqualTo("room-1"));
            Assert.That(photos[0].FileName, Is.EqualTo("photo.jpg"));
            Assert.That(photos[0].Url, Is.EqualTo("/uploads/rooms/room-1/photo.jpg"));
            Assert.That(photos[0].ContentType, Is.EqualTo("image/jpeg"));
            Assert.That(photos[0].SizeBytes, Is.EqualTo(1234));
            Assert.That(photos[0].UploadedByUserId, Is.EqualTo("landlord-1"));
            Assert.That(photos[0].UploadedAtUtc, Is.EqualTo(BaseUtc));
        });
    }

    [Test]
    public async Task ListByRoomAsync_ReturnsOnlyPhotosOfRequestedRoom()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IRoomPhotoRepository repository = new SqliteRoomPhotoRepository(dbContext);

        await repository.AddAsync(CreatePhoto("room-a", "a-1", BaseUtc));
        await repository.AddAsync(CreatePhoto("room-b", "b-1", BaseUtc.AddMinutes(1)));
        await repository.AddAsync(CreatePhoto("room-a", "a-2", BaseUtc.AddMinutes(2)));

        var roomAPhotos = await repository.ListByRoomAsync("room-a");
        var roomBPhotos = await repository.ListByRoomAsync("room-b");
        var missingRoomPhotos = await repository.ListByRoomAsync("room-c");

        Assert.Multiple(() =>
        {
            Assert.That(roomAPhotos.Select(p => p.Id), Is.EqualTo(new[] { "a-1", "a-2" }));
            Assert.That(roomBPhotos.Count, Is.EqualTo(1));
            Assert.That(roomBPhotos[0].Id, Is.EqualTo("b-1"));
            Assert.That(missingRoomPhotos, Is.Empty);
        });
    }

    [Test]
    public async Task GetAsync_ReturnsPhotoOnlyForMatchingRoomAndPhotoId()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IRoomPhotoRepository repository = new SqliteRoomPhotoRepository(dbContext);

        await repository.AddAsync(CreatePhoto("room-1", "photo-1", BaseUtc));

        var found = await repository.GetAsync("room-1", "photo-1");
        var wrongRoom = await repository.GetAsync("room-2", "photo-1");
        var missingPhoto = await repository.GetAsync("room-1", "photo-x");

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.Not.Null);
            Assert.That(found!.Id, Is.EqualTo("photo-1"));
            Assert.That(found.Url, Is.EqualTo("/uploads/rooms/room-1/photo.jpg"));
            Assert.That(wrongRoom, Is.Null);
            Assert.That(missingPhoto, Is.Null);
        });
    }

    [Test]
    public async Task DeleteAsync_RemovesPhotoFromStore()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IRoomPhotoRepository repository = new SqliteRoomPhotoRepository(dbContext);

        await repository.AddAsync(CreatePhoto("room-1", "photo-1", BaseUtc));
        await repository.AddAsync(CreatePhoto("room-1", "photo-2", BaseUtc.AddMinutes(1)));

        await repository.DeleteAsync("photo-1");

        var deleted = await repository.GetAsync("room-1", "photo-1");
        var remaining = await repository.ListByRoomAsync("room-1");

        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.Null);
            Assert.That(remaining.Count, Is.EqualTo(1));
            Assert.That(remaining[0].Id, Is.EqualTo("photo-2"));
        });
    }

    [Test]
    public void AddSqliteDatastore_RegistersSqliteRoomPhotoRepository()
    {
        var services = new ServiceCollection();
        services.AddSqliteDatastore(new SqliteSettings { ConnectionString = "Data Source=:memory:" });

        var descriptor = services.Single(d => d.ServiceType == typeof(IRoomPhotoRepository));

        Assert.That(descriptor.ImplementationType, Is.EqualTo(typeof(SqliteRoomPhotoRepository)));
    }

    [Test]
    public void AddPhotoInfrastructure_RegistersStorageAndFallsBackToInMemoryRepository()
    {
        var services = new ServiceCollection();
        services.AddPhotoInfrastructure(CreateTempUploadsPath());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var storage = provider.GetRequiredService<IPhotoStoragePort>();
        var repository = scope.ServiceProvider.GetRequiredService<IRoomPhotoRepository>();

        Assert.Multiple(() =>
        {
            Assert.That(storage, Is.TypeOf<LocalDiskPhotoStorageAdapter>());
            Assert.That(repository, Is.TypeOf<InMemoryRoomPhotoRepository>());
        });
    }

    [Test]
    public void AddPhotoInfrastructure_AfterSqliteDatastore_KeepsSqliteRepository()
    {
        var services = new ServiceCollection();
        services.AddSqliteDatastore(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        services.AddPhotoInfrastructure(CreateTempUploadsPath());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IRoomPhotoRepository>();

        Assert.That(repository, Is.TypeOf<SqliteRoomPhotoRepository>());
    }
}
