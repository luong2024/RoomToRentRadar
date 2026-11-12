using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Datastore.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Datastore.Sqlite.Repositories;
using RoomToRentRadar.Datastore.Sqlite.Transactions;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Infrastructure;
using RoomToRentRadar.Infrastructure.Adapters.Storage;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Ports.Outbound.Transactions;

namespace RoomToRentRadarUnitTests.Infrastructure;

[TestFixture]
public class SqliteRoomPhotoRepositoryTests
{
    private static RoomPhoto CreatePhoto(
        string id,
        string uploadedByUserId = "landlord-1",
        string? roomId = null,
        int sortOrder = 0,
        PhotoKind kind = PhotoKind.Standard,
        DateTime? createdAtUtc = null)
        => new(
            uploadedByUserId: uploadedByUserId,
            kind: kind,
            contentType: "image/jpeg",
            sizeBytes: 1234,
            width: 800,
            height: 600,
            originalPath: $"rooms/2026/11/{id}/original.jpg",
            mediumPath: $"rooms/2026/11/{id}/medium.jpg",
            thumbPath: $"rooms/2026/11/{id}/thumb.jpg",
            roomId: roomId,
            sortOrder: sortOrder,
            id: id,
            createdAtUtc: createdAtUtc ?? DateTime.UtcNow);

    private static string CreateTempUploadsPath()
    {
        return Path.Combine(Path.GetTempPath(), "rtrr-photos", Guid.NewGuid().ToString("N"));
    }

    [Test]
    public async Task AddAsync_ThenGetOwnedAsync_ReturnsFullPhotoForOwner()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IRoomPhotoRepository repository = new SqliteRoomPhotoRepository(dbContext, new SqliteUnitOfWork(dbContext));
        var createdAtUtc = new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc);

        await repository.AddAsync(CreatePhoto("photo-1", createdAtUtc: createdAtUtc));

        var stored = await repository.GetOwnedAsync("photo-1", "landlord-1");

        Assert.That(stored, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(stored!.Id, Is.EqualTo("photo-1"));
            Assert.That(stored.RoomId, Is.Null);
            Assert.That(stored.UploadedByUserId, Is.EqualTo("landlord-1"));
            Assert.That(stored.Kind, Is.EqualTo(PhotoKind.Standard));
            Assert.That(stored.ContentType, Is.EqualTo("image/jpeg"));
            Assert.That(stored.SizeBytes, Is.EqualTo(1234));
            Assert.That(stored.Width, Is.EqualTo(800));
            Assert.That(stored.Height, Is.EqualTo(600));
            Assert.That(stored.OriginalPath, Is.EqualTo("rooms/2026/11/photo-1/original.jpg"));
            Assert.That(stored.MediumPath, Is.EqualTo("rooms/2026/11/photo-1/medium.jpg"));
            Assert.That(stored.ThumbPath, Is.EqualTo("rooms/2026/11/photo-1/thumb.jpg"));
            Assert.That(stored.SortOrder, Is.EqualTo(0));
            Assert.That(stored.CreatedAtUtc, Is.EqualTo(createdAtUtc));
            Assert.That(stored.AttachedAtUtc, Is.Null);
        });
    }

    [Test]
    public async Task GetOwnedAsync_WithOtherUser_ReturnsNull()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IRoomPhotoRepository repository = new SqliteRoomPhotoRepository(dbContext, new SqliteUnitOfWork(dbContext));

        await repository.AddAsync(CreatePhoto("photo-1"));

        var stored = await repository.GetOwnedAsync("photo-1", "landlord-2");

        Assert.That(stored, Is.Null);
    }

    [Test]
    public async Task GetOwnedAsync_WithMissingPhoto_ReturnsNull()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IRoomPhotoRepository repository = new SqliteRoomPhotoRepository(dbContext, new SqliteUnitOfWork(dbContext));

        await repository.AddAsync(CreatePhoto("photo-1"));

        var stored = await repository.GetOwnedAsync("photo-x", "landlord-1");

        Assert.That(stored, Is.Null);
    }

    [Test]
    public async Task DeleteUnattachedAsync_WhenOwnerAndUnattached_DeletesAndReturnsTrue()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IRoomPhotoRepository repository = new SqliteRoomPhotoRepository(dbContext, new SqliteUnitOfWork(dbContext));
        await repository.AddAsync(CreatePhoto("photo-1"));

        var deleted = await repository.DeleteUnattachedAsync("photo-1", "landlord-1");

        Assert.That(deleted, Is.True);
        Assert.That(await repository.GetOwnedAsync("photo-1", "landlord-1"), Is.Null);
    }

    [Test]
    public async Task DeleteUnattachedAsync_WhenPhotoAttached_ReturnsFalseAndKeepsPhoto()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IRoomPhotoRepository repository = new SqliteRoomPhotoRepository(dbContext, new SqliteUnitOfWork(dbContext));
        await repository.AddAsync(CreatePhoto("photo-1"));
        await repository.AttachAsync("landlord-1", "room-1", new[] { "photo-1" });

        var deleted = await repository.DeleteUnattachedAsync("photo-1", "landlord-1");
        var stored = await repository.GetOwnedAsync("photo-1", "landlord-1");

        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.False);
            Assert.That(stored, Is.Not.Null);
            Assert.That(stored!.RoomId, Is.EqualTo("room-1"));
        });
    }

    [Test]
    public async Task DeleteUnattachedAsync_WithOtherUser_ReturnsFalseAndKeepsPhoto()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IRoomPhotoRepository repository = new SqliteRoomPhotoRepository(dbContext, new SqliteUnitOfWork(dbContext));
        await repository.AddAsync(CreatePhoto("photo-1"));

        var deleted = await repository.DeleteUnattachedAsync("photo-1", "landlord-2");
        var stored = await repository.GetOwnedAsync("photo-1", "landlord-1");

        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.False);
            Assert.That(stored, Is.Not.Null);
        });
    }

    [Test]
    public async Task AttachAsync_WithOwnedUnattachedPhotos_ReturnsCountAndSetsRoomAndOrder()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IRoomPhotoRepository repository = new SqliteRoomPhotoRepository(dbContext, new SqliteUnitOfWork(dbContext));
        await repository.AddAsync(CreatePhoto("p0"));
        await repository.AddAsync(CreatePhoto("p1"));
        await repository.AddAsync(CreatePhoto("p2"));

        var updated = await repository.AttachAsync("landlord-1", "room-1", new[] { "p1", "p2", "p0" });
        var photos = await repository.ListByRoomAsync("room-1");

        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.EqualTo(3));
            Assert.That(photos.Select(p => p.Id), Is.EqualTo(new[] { "p1", "p2", "p0" }));
            Assert.That(photos.Select(p => p.RoomId), Is.All.EqualTo("room-1"));
            Assert.That(photos.Select(p => p.SortOrder), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(photos.Select(p => p.AttachedAtUtc), Is.All.Not.Null);
        });
    }

    [Test]
    public async Task AttachAsync_WithMixedOwnership_ReturnsOnlyUpdatedRows()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IRoomPhotoRepository repository = new SqliteRoomPhotoRepository(dbContext, new SqliteUnitOfWork(dbContext));
        await repository.AddAsync(CreatePhoto("mine"));
        await repository.AddAsync(CreatePhoto("theirs", uploadedByUserId: "landlord-2"));
        await repository.AddAsync(CreatePhoto("elsewhere", roomId: "room-x", sortOrder: 3));

        var updated = await repository.AttachAsync(
            "landlord-1",
            "room-1",
            new[] { "mine", "theirs", "elsewhere" });

        var mine = await repository.GetOwnedAsync("mine", "landlord-1");
        var theirs = await repository.GetOwnedAsync("theirs", "landlord-2");
        var elsewhere = await repository.GetOwnedAsync("elsewhere", "landlord-1");
        var roomXPhotos = await repository.ListByRoomAsync("room-x");

        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.EqualTo(1));
            Assert.That(mine!.RoomId, Is.EqualTo("room-1"));
            Assert.That(mine.SortOrder, Is.EqualTo(0));
            Assert.That(theirs!.RoomId, Is.Null);
            Assert.That(theirs.SortOrder, Is.EqualTo(0));
            Assert.That(theirs.AttachedAtUtc, Is.Null);
            Assert.That(elsewhere!.RoomId, Is.EqualTo("room-x"));
            Assert.That(elsewhere.SortOrder, Is.EqualTo(3));
            Assert.That(elsewhere.AttachedAtUtc, Is.Null);
            Assert.That(roomXPhotos.Select(p => p.Id), Is.EqualTo(new[] { "elsewhere" }));
        });
    }

    [Test]
    public async Task AttachAsync_ReattachToSameRoom_UpdatesSortOrder()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IRoomPhotoRepository repository = new SqliteRoomPhotoRepository(dbContext, new SqliteUnitOfWork(dbContext));
        await repository.AddAsync(CreatePhoto("photo-1"));
        await repository.AddAsync(CreatePhoto("photo-2"));

        var first = await repository.AttachAsync("landlord-1", "room-1", new[] { "photo-1", "photo-2" });
        var second = await repository.AttachAsync("landlord-1", "room-1", new[] { "photo-2", "photo-1" });
        var photos = await repository.ListByRoomAsync("room-1");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(2));
            Assert.That(second, Is.EqualTo(2));
            Assert.That(photos.Select(p => p.Id), Is.EqualTo(new[] { "photo-2", "photo-1" }));
            Assert.That(photos.Select(p => p.SortOrder), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(photos.Select(p => p.AttachedAtUtc), Is.All.Not.Null);
        });
    }

    [Test]
    public async Task AttachAsync_WithEmptyIds_ReturnsZero()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IRoomPhotoRepository repository = new SqliteRoomPhotoRepository(dbContext, new SqliteUnitOfWork(dbContext));
        await repository.AddAsync(CreatePhoto("photo-1"));

        var updated = await repository.AttachAsync("landlord-1", "room-1", Array.Empty<string>());
        var stored = await repository.GetOwnedAsync("photo-1", "landlord-1");

        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.EqualTo(0));
            Assert.That(stored!.RoomId, Is.Null);
        });
    }

    [Test]
    public async Task AttachAsync_WithExpiredUnattachedPhoto_DoesNotAttach()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IRoomPhotoRepository repository = new SqliteRoomPhotoRepository(dbContext, new SqliteUnitOfWork(dbContext));
        await repository.AddAsync(CreatePhoto("old", createdAtUtc: DateTime.UtcNow.AddHours(-25)));

        var updated = await repository.AttachAsync("landlord-1", "room-1", new[] { "old" });
        var stored = await repository.GetOwnedAsync("old", "landlord-1");

        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.EqualTo(0));
            Assert.That(stored!.RoomId, Is.Null);
            Assert.That(stored.AttachedAtUtc, Is.Null);
        });
    }

    [Test]
    public async Task AttachAsync_WithPhotoJustInsideLifetime_Attaches()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IRoomPhotoRepository repository = new SqliteRoomPhotoRepository(dbContext, new SqliteUnitOfWork(dbContext));
        await repository.AddAsync(CreatePhoto("fresh", createdAtUtc: DateTime.UtcNow.AddHours(-23)));

        var updated = await repository.AttachAsync("landlord-1", "room-1", new[] { "fresh" });

        Assert.That(updated, Is.EqualTo(1));
    }

    [Test]
    public async Task AttachAsync_WithOldPhotoAlreadyInSameRoom_Reattaches()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IRoomPhotoRepository repository = new SqliteRoomPhotoRepository(dbContext, new SqliteUnitOfWork(dbContext));
        await repository.AddAsync(CreatePhoto(
            "old", roomId: "room-1", sortOrder: 5, createdAtUtc: DateTime.UtcNow.AddHours(-25)));

        var updated = await repository.AttachAsync("landlord-1", "room-1", new[] { "old" });
        var stored = await repository.GetOwnedAsync("old", "landlord-1");

        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.EqualTo(1));
            Assert.That(stored!.RoomId, Is.EqualTo("room-1"));
            Assert.That(stored.SortOrder, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task AttachAsync_InsideUnitOfWork_CommitsWhenWorkSucceeds()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        var unitOfWork = new SqliteUnitOfWork(dbContext);
        IRoomPhotoRepository repository = new SqliteRoomPhotoRepository(dbContext, unitOfWork);
        await repository.AddAsync(CreatePhoto("photo-1"));
        await repository.AddAsync(CreatePhoto("photo-2"));

        var updated = await unitOfWork.ExecuteAsync(async ct =>
            await repository.AttachAsync("landlord-1", "room-1", new[] { "photo-1", "photo-2" }, ct));

        var photos = await repository.ListByRoomAsync("room-1");

        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.EqualTo(2));
            Assert.That(photos.Select(p => p.Id), Is.EqualTo(new[] { "photo-1", "photo-2" }));
            Assert.That(photos.Select(p => p.SortOrder), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(photos.Select(p => p.AttachedAtUtc), Is.All.Not.Null);
        });
    }

    [Test]
    public async Task AttachAsync_WithExpiredIdInCallerTransaction_RollsBackWholeAttach()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        var unitOfWork = new SqliteUnitOfWork(dbContext);
        IRoomPhotoRepository repository = new SqliteRoomPhotoRepository(dbContext, unitOfWork);
        await repository.AddAsync(CreatePhoto("fresh"));
        await repository.AddAsync(CreatePhoto("expired", createdAtUtc: DateTime.UtcNow.AddHours(-25)));

        var updated = 0;
        await Assert.ThatAsync(
            async () => await unitOfWork.ExecuteAsync<int>(async ct =>
            {
                updated = await repository.AttachAsync("landlord-1", "room-1", new[] { "fresh", "expired" }, ct);
                throw new InvalidOperationException("400 VALIDATION_FAILED");
            }),
            Throws.TypeOf<InvalidOperationException>());

        var fresh = await repository.GetOwnedAsync("fresh", "landlord-1");

        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.EqualTo(1));
            Assert.That(fresh!.RoomId, Is.Null);
            Assert.That(fresh.AttachedAtUtc, Is.Null);
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenWorkThrowsAndTokenCancelled_RethrowsOriginalException()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        var unitOfWork = new SqliteUnitOfWork(dbContext);
        IRoomPhotoRepository repository = new SqliteRoomPhotoRepository(dbContext, unitOfWork);
        await repository.AddAsync(CreatePhoto("photo-1"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThatAsync(
            async () => await unitOfWork.ExecuteAsync<int>(async _ =>
            {
                await repository.AttachAsync("landlord-1", "room-1", new[] { "photo-1" }, CancellationToken.None);
                throw new DomainValidationException("400 VALIDATION_FAILED");
            }, cts.Token),
            Throws.TypeOf<DomainValidationException>());

        var stored = await repository.GetOwnedAsync("photo-1", "landlord-1");

        Assert.Multiple(() =>
        {
            Assert.That(stored!.RoomId, Is.Null);
            Assert.That(stored.AttachedAtUtc, Is.Null);
        });
    }

    [Test]
    public async Task ListByRoomAsync_ReturnsOnlyRoomPhotosOrderedBySortOrder()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        IRoomPhotoRepository repository = new SqliteRoomPhotoRepository(dbContext, new SqliteUnitOfWork(dbContext));
        await repository.AddAsync(CreatePhoto("photo-2", roomId: "room-1", sortOrder: 2));
        await repository.AddAsync(CreatePhoto("photo-0", roomId: "room-1", sortOrder: 0));
        await repository.AddAsync(CreatePhoto("photo-1", roomId: "room-1", sortOrder: 1));
        await repository.AddAsync(CreatePhoto("other", roomId: "room-2", sortOrder: 0));

        var roomOne = await repository.ListByRoomAsync("room-1");
        var roomTwo = await repository.ListByRoomAsync("room-2");

        Assert.Multiple(() =>
        {
            Assert.That(roomOne.Select(p => p.Id), Is.EqualTo(new[] { "photo-0", "photo-1", "photo-2" }));
            Assert.That(roomOne.Select(p => p.RoomId), Is.All.EqualTo("room-1"));
            Assert.That(roomTwo.Select(p => p.Id), Is.EqualTo(new[] { "other" }));
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
    public void AddSqliteDatastore_RegistersUnitOfWork()
    {
        var services = new ServiceCollection();
        services.AddSqliteDatastore(new SqliteSettings { ConnectionString = "Data Source=:memory:" });

        var descriptor = services.Single(d => d.ServiceType == typeof(IUnitOfWork));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var concrete = scope.ServiceProvider.GetRequiredService<SqliteUnitOfWork>();

        Assert.Multiple(() =>
        {
            Assert.That(descriptor.Lifetime, Is.EqualTo(ServiceLifetime.Scoped));
            Assert.That(unitOfWork, Is.TypeOf<SqliteUnitOfWork>());
            Assert.That(concrete, Is.SameAs(unitOfWork));
        });
    }

    [Test]
    public void AddPhotoInfrastructure_RegistersLocalDiskStorage()
    {
        var services = new ServiceCollection();
        services.AddPhotoInfrastructure(CreateTempUploadsPath(), "http://localhost:5000");

        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IPhotoStoragePort>();

        Assert.Multiple(() =>
        {
            Assert.That(storage, Is.TypeOf<LocalDiskPhotoStorageAdapter>());
            Assert.That(services.All(d => d.ServiceType != typeof(IRoomPhotoRepository)), Is.True);
        });
    }

    [Test]
    public void AddPhotoInfrastructure_AfterSqliteDatastore_KeepsSqliteRepository()
    {
        var services = new ServiceCollection();
        services.AddSqliteDatastore(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        services.AddPhotoInfrastructure(CreateTempUploadsPath(), "http://localhost:5000");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IRoomPhotoRepository>();

        Assert.That(repository, Is.TypeOf<SqliteRoomPhotoRepository>());
    }
}
