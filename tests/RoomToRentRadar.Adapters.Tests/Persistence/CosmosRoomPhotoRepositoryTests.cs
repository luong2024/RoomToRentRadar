using System.Net;
using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Adapters.Tests.Persistence;

[TestFixture]
public class CosmosRoomPhotoRepositoryTests
{
    private CosmosDbContext _dbContext = null!;
    private InMemoryCosmosContainer<RoomPhotoDocument> _container = null!;

    [SetUp]
    public void SetUp()
    {
        _dbContext = CosmosDbContext.CreateInMemory();
        _container = (InMemoryCosmosContainer<RoomPhotoDocument>)_dbContext.RoomPhotos;
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    [Test]
    public async Task AddWithinQuotaAsync_ConcurrentCoverUploadsFromSeparateRepositoryInstances_LeavesSingleCover()
    {
        // Two separate repository instances sharing the same persistence container (simulating 2 API instances)
        var instanceA = new CosmosRoomPhotoRepository(_dbContext);
        var instanceB = new CosmosRoomPhotoRepository(_dbContext);

        // Seed initial cover photo
        await instanceA.AddWithinQuotaAsync(
            "room-001",
            RoomPhoto.MaxPhotosPerRoom,
            requestCover: true,
            (order, isCover) => CreatePhoto("photo-initial", "room-001", order, isCover));

        // Force both instances to read the exact same stale snapshot before executing their transactional batch
        var bothReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        _container.OnBeforeBatchAsync = async _ =>
        {
            if (Interlocked.Increment(ref arrived) <= 2)
            {
                if (arrived == 2)
                {
                    bothReady.TrySetResult();
                }

                await bothReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
        };

        var taskA = instanceA.AddWithinQuotaAsync(
            "room-001",
            RoomPhoto.MaxPhotosPerRoom,
            requestCover: true,
            (order, isCover) => CreatePhoto("photo-from-a", "room-001", order, isCover));

        var taskB = instanceB.AddWithinQuotaAsync(
            "room-001",
            RoomPhoto.MaxPhotosPerRoom,
            requestCover: true,
            (order, isCover) => CreatePhoto("photo-from-b", "room-001", order, isCover));

        await Task.WhenAll(taskA, taskB);

        var photos = await instanceA.GetByRoomIdAsync("room-001");
        Assert.Multiple(() =>
        {
            Assert.That(photos, Has.Count.EqualTo(3));
            Assert.That(photos.Count(p => p.IsCover), Is.EqualTo(1), "Exactly one cover photo must exist after concurrent multi-instance cover uploads.");
            Assert.That(photos.First(p => p.Id == "photo-initial").IsCover, Is.False);
            Assert.That(photos.Select(p => p.DisplayOrder).Distinct().Count(), Is.EqualTo(3));
        });
    }

    [Test]
    public async Task AddWithinQuotaAsync_ConcurrentFinalSlotFromSeparateRepositoryInstances_NeverExceedsQuota()
    {
        var instanceA = new CosmosRoomPhotoRepository(_dbContext);
        var instanceB = new CosmosRoomPhotoRepository(_dbContext);

        for (var i = 0; i < RoomPhoto.MaxPhotosPerRoom - 1; i++)
        {
            await instanceA.AddWithinQuotaAsync(
                "room-001",
                RoomPhoto.MaxPhotosPerRoom,
                requestCover: false,
                (order, isCover) => CreatePhoto($"seed-{i}", "room-001", order, isCover));
        }

        var bothReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        _container.OnBeforeBatchAsync = async _ =>
        {
            if (Interlocked.Increment(ref arrived) <= 2)
            {
                if (arrived == 2)
                {
                    bothReady.TrySetResult();
                }

                await bothReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
        };

        var taskA = instanceA.AddWithinQuotaAsync(
            "room-001",
            RoomPhoto.MaxPhotosPerRoom,
            requestCover: true,
            (order, isCover) => CreatePhoto("race-a", "room-001", order, isCover));

        var taskB = instanceB.AddWithinQuotaAsync(
            "room-001",
            RoomPhoto.MaxPhotosPerRoom,
            requestCover: true,
            (order, isCover) => CreatePhoto("race-b", "room-001", order, isCover));

        var outcomes = await Task.WhenAll(
            taskA.ContinueWith<object>(t => t.IsFaulted ? t.Exception!.InnerException! : t.Result),
            taskB.ContinueWith<object>(t => t.IsFaulted ? t.Exception!.InnerException! : t.Result));

        var photos = await instanceA.GetByRoomIdAsync("room-001");
        Assert.Multiple(() =>
        {
            Assert.That(outcomes.OfType<RoomPhoto>().Count(), Is.EqualTo(1));
            Assert.That(outcomes.OfType<RoomPhotoQuotaExceededException>().Count(), Is.EqualTo(1));
            Assert.That(photos, Has.Count.EqualTo(RoomPhoto.MaxPhotosPerRoom));
            Assert.That(photos.Count(p => p.IsCover), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ReorderAsync_WhenPhotoIsDeletedConcurrently_DoesNotResurrectDeletedPhoto()
    {
        var repo = new CosmosRoomPhotoRepository(_dbContext);
        var p1 = await repo.AddWithinQuotaAsync("room-001", 12, true, (o, c) => CreatePhoto("p1", "room-001", o, c));
        var p2 = await repo.AddWithinQuotaAsync("room-001", 12, false, (o, c) => CreatePhoto("p2", "room-001", o, c));

        // Simulate a concurrent delete completing right after ReorderAsync read [p1, p2] and before its batch runs
        var deletedOnce = false;
        _container.OnBeforeBatchAsync = async _ =>
        {
            if (!deletedOnce)
            {
                deletedOnce = true;
                await _container.DeleteItemAsync(p2.Id, "room-001");
            }
        };

        Assert.ThrowsAsync<RoomPhotoNotFoundException>(() =>
            repo.ReorderAsync("room-001", [p2.Id, p1.Id], p2.Id));

        var photos = await repo.GetByRoomIdAsync("room-001");
        Assert.Multiple(() =>
        {
            Assert.That(photos, Has.Count.EqualTo(1));
            Assert.That(photos[0].Id, Is.EqualTo(p1.Id));
            Assert.That(photos[0].IsCover, Is.True);
        });
    }

    [Test]
    public async Task ReorderAsync_WhenBatchFailsMidUpdate_LeavesGalleryCompletelyUnchanged()
    {
        var repo = new CosmosRoomPhotoRepository(_dbContext);
        var p1 = await repo.AddWithinQuotaAsync("room-001", 12, true, (o, c) => CreatePhoto("p1", "room-001", o, c));
        var p2 = await repo.AddWithinQuotaAsync("room-001", 12, false, (o, c) => CreatePhoto("p2", "room-001", o, c));

        _container.OnBeforeBatchAsync = _ => throw new CosmosException(
            "Simulated transient backend failure during batch.",
            HttpStatusCode.InternalServerError,
            0,
            "activity-1",
            1.0);

        Assert.ThrowsAsync<CosmosException>(() =>
            repo.ReorderAsync("room-001", [p2.Id, p1.Id], p2.Id));

        _container.OnBeforeBatchAsync = null;
        var photos = await repo.GetByRoomIdAsync("room-001");
        Assert.Multiple(() =>
        {
            Assert.That(photos, Has.Count.EqualTo(2));
            Assert.That(photos[0].Id, Is.EqualTo(p1.Id));
            Assert.That(photos[0].DisplayOrder, Is.EqualTo(0));
            Assert.That(photos[0].IsCover, Is.True);
            Assert.That(photos[1].Id, Is.EqualTo(p2.Id));
            Assert.That(photos[1].DisplayOrder, Is.EqualTo(1));
            Assert.That(photos[1].IsCover, Is.False);
        });
    }

    [Test]
    public async Task ExhaustedConcurrencyRetries_ThrowsConcurrencyConflictExceptionForUploadReorderAndDelete()
    {
        var repo = new CosmosRoomPhotoRepository(_dbContext);
        var p1 = await repo.AddWithinQuotaAsync("room-001", 12, true, (o, c) => CreatePhoto("p1", "room-001", o, c));
        var p2 = await repo.AddWithinQuotaAsync("room-001", 12, false, (o, c) => CreatePhoto("p2", "room-001", o, c));

        // Force every transactional batch attempt (including the final 12th attempt) to fail with 412 PreconditionFailed.
        _container.OnBeforeBatchAsync = _ => throw new CosmosException(
            "Simulated persistent ETag conflict.",
            HttpStatusCode.PreconditionFailed,
            0,
            "activity-412",
            1.0);

        Assert.Multiple(() =>
        {
            Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
                repo.AddWithinQuotaAsync("room-001", 12, false, (o, c) => CreatePhoto("p3", "room-001", o, c)));
            Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
                repo.ReorderAsync("room-001", [p2.Id, p1.Id], p2.Id));
            Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
                repo.MarkPendingDeletionAsync("room-001", p1.Id));
        });
    }

    [Test]
    public async Task MarkPendingDeletionAsync_WhenAlreadyPendingDeletion_ReturnsPendingPhotoForRetryableDelete()
    {
        var repo = new CosmosRoomPhotoRepository(_dbContext);
        var p1 = await repo.AddWithinQuotaAsync("room-001", 12, true, (o, c) => CreatePhoto("p1", "room-001", o, c));

        var firstMark = await repo.MarkPendingDeletionAsync("room-001", p1.Id);
        var retryMark = await repo.MarkPendingDeletionAsync("room-001", p1.Id);

        Assert.Multiple(() =>
        {
            Assert.That(firstMark, Is.Not.Null);
            Assert.That(retryMark, Is.Not.Null);
            Assert.That(retryMark!.Id, Is.EqualTo(p1.Id));
            Assert.That(retryMark.ObjectKey, Is.EqualTo(p1.ObjectKey));
        });
    }

    private static RoomPhoto CreatePhoto(string id, string roomId, int displayOrder, bool isCover) =>
        RoomPhoto.Create(
            id: id,
            roomId: roomId,
            url: $"https://cdn.test/rooms/{roomId}/{id}.jpg",
            thumbnailUrl: $"https://cdn.test/rooms/{roomId}/{id}_thumb.jpg",
            objectKey: $"rooms/{roomId}/{id}.jpg",
            thumbnailObjectKey: $"rooms/{roomId}/{id}_thumb.jpg",
            contentType: "image/jpeg",
            sizeBytes: 1024,
            width: 1920,
            height: 1080,
            is360: false,
            isCover: isCover,
            displayOrder: displayOrder,
            caption: null,
            uploadedByUserId: Guid.NewGuid(),
            uploadedAtUtc: DateTimeOffset.UtcNow);
}
