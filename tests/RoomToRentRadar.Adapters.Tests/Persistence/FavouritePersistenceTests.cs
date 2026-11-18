using System.Net;
using Microsoft.Azure.Cosmos;
using Moq;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Adapters.Outbound.Persistence.InMemory;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Adapters.Tests.Persistence;

[TestFixture]
public class FavouritePersistenceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    private CosmosDbContext _dbContext = null!;
    private CosmosFavouriteRepository _repository = null!;

    [SetUp]
    public async Task SetUp()
    {
        _dbContext = CosmosDbContext.CreateInMemory();
        await _dbContext.EnsureDatabaseAndContainersAsync();
        _repository = new CosmosFavouriteRepository(_dbContext);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    [Test]
    public async Task AddIfNotExistsAsync_WhenNewFavourite_ShouldCreateAndReturnWasCreatedTrue()
    {
        var userId = Guid.NewGuid();
        var fav = FavouriteRoom.Create(userId, "Room-ICTU-001", FixedNow);

        var (saved, wasCreated) = await _repository.AddIfNotExistsAsync(fav);

        Assert.Multiple(() =>
        {
            Assert.That(wasCreated, Is.True);
            Assert.That(saved.UserId, Is.EqualTo(userId));
            Assert.That(saved.RoomId, Is.EqualTo("Room-ICTU-001"));
            Assert.That(saved.CreatedAtUtc, Is.EqualTo(FixedNow));
        });
    }

    [Test]
    public async Task AddIfNotExistsAsync_WhenDuplicateOrConcurrentConflictOccurs_ShouldCatch409AndReturnExistingWithWasCreatedFalse()
    {
        var userId = Guid.NewGuid();
        var originalFav = FavouriteRoom.Create(userId, "Room-ICTU-001", FixedNow);
        var duplicateFav = FavouriteRoom.Create(userId, "room-ictu-001", FixedNow.AddMinutes(5));

        var (_, firstCreated) = await _repository.AddIfNotExistsAsync(originalFav);
        var (secondResult, secondCreated) = await _repository.AddIfNotExistsAsync(duplicateFav);

        Assert.Multiple(() =>
        {
            Assert.That(firstCreated, Is.True);
            Assert.That(secondCreated, Is.False);
            Assert.That(secondResult.RoomId, Is.EqualTo("Room-ICTU-001"));
            Assert.That(secondResult.CreatedAtUtc, Is.EqualTo(FixedNow));
        });
    }

    [Test]
    public async Task TryAddWithQuotaAsync_WhenMultipleRepositoryInstancesWriteConcurrentlyNearLimit_ShouldEnforceQuotaViaCosmosETag()
    {
        var userId = Guid.NewGuid();
        const int maxQuota = 10;

        // Pre-populate 5 items
        for (var i = 1; i <= 5; i++)
        {
            var preFav = FavouriteRoom.Create(userId, $"room-init-{i}", FixedNow.AddSeconds(i));
            await _repository.TryAddWithQuotaAsync(preFav, maxQuota);
        }

        // Launch 20 concurrent writes across 20 separate CosmosFavouriteRepository instances
        // (simulating multiple independent API instances sharing only Cosmos DB / ETag state)
        using var startGate = new ManualResetEventSlim(false);
        var tasks = Enumerable.Range(1, 20).Select(i => Task.Run(async () =>
        {
            var independentRepoInstance = new CosmosFavouriteRepository(_dbContext);
            startGate.Wait();
            var fav = FavouriteRoom.Create(userId, $"room-concurrent-{i}", FixedNow.AddMinutes(i));
            return await independentRepoInstance.TryAddWithQuotaAsync(fav, maxQuota);
        })).ToArray();

        startGate.Set();
        var results = await Task.WhenAll(tasks);

        var createdCount = results.Count(r => r.Status == FavouriteAddStatus.Created);
        var quotaExceededCount = results.Count(r => r.Status == FavouriteAddStatus.QuotaExceeded);
        var finalStored = await _repository.GetByUserIdAsync(userId);

        Assert.Multiple(() =>
        {
            Assert.That(createdCount, Is.EqualTo(5), "Only 5 additional rooms should succeed to reach maxQuota = 10.");
            Assert.That(quotaExceededCount, Is.EqualTo(15), "Remaining 15 concurrent writes must be rejected with QuotaExceeded.");
            Assert.That(finalStored, Has.Count.EqualTo(maxQuota), "Total persisted favourites for user must never exceed maxQuota.");
        });
    }

    [Test]
    public async Task EnsureFavouritesContainerAsync_ShouldBeInvokedDynamicallyOnRepositoryOperations()
    {
        var userId = Guid.NewGuid();
        var mockDbContext = new Mock<ICosmosDbContext>();
        var inMemoryContainer = new InMemoryCosmosContainer<FavouriteDocument>();

        mockDbContext.Setup(c => c.Favourites).Returns(inMemoryContainer);
        mockDbContext
            .Setup(c => c.EnsureFavouritesContainerAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var repo = new CosmosFavouriteRepository(mockDbContext.Object);
        var fav = FavouriteRoom.Create(userId, "room-ictu-001", FixedNow);

        await repo.AddIfNotExistsAsync(fav);
        await repo.GetByUserIdAsync(userId);
        await repo.RemoveAsync(userId, "room-ictu-001");

        mockDbContext.Verify(
            c => c.EnsureFavouritesContainerAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Test]
    public async Task GetByUserIdAsync_ShouldIsolateFavouritesBetweenUsersAndOrderByCreatedAtDescending()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await _repository.AddIfNotExistsAsync(FavouriteRoom.Create(userA, "room-1", FixedNow));
        await _repository.AddIfNotExistsAsync(FavouriteRoom.Create(userA, "room-2", FixedNow.AddMinutes(10)));
        await _repository.AddIfNotExistsAsync(FavouriteRoom.Create(userB, "room-3", FixedNow.AddMinutes(20)));

        var userAFavourites = await _repository.GetByUserIdAsync(userA);
        var userBFavourites = await _repository.GetByUserIdAsync(userB);

        Assert.Multiple(() =>
        {
            Assert.That(userAFavourites, Has.Count.EqualTo(2));
            Assert.That(userAFavourites[0].RoomId, Is.EqualTo("room-2"));
            Assert.That(userAFavourites[1].RoomId, Is.EqualTo("room-1"));

            Assert.That(userBFavourites, Has.Count.EqualTo(1));
            Assert.That(userBFavourites[0].RoomId, Is.EqualTo("room-3"));
        });
    }

    [Test]
    public async Task RemoveAsync_WhenRecordExistsOrDoesNotExist_ShouldBeIdempotent()
    {
        var userId = Guid.NewGuid();
        await _repository.AddIfNotExistsAsync(FavouriteRoom.Create(userId, "room-1", FixedNow));

        await _repository.RemoveAsync(userId, "ROOM-1");
        var afterFirstRemove = await _repository.GetByUserAndRoomIdAsync(userId, "room-1");

        // Second remove on non-existent item must not throw
        Assert.DoesNotThrowAsync(async () => await _repository.RemoveAsync(userId, "room-1"));
        Assert.That(afterFirstRemove, Is.Null);
    }

    [Test]
    public async Task InMemoryRoomRepository_GetByIdsAsync_ShouldReturnMatchingRoomsCaseInsensitivelyAndDeduplicate()
    {
        var repo = new InMemoryRoomRepository(new[]
        {
            new Room { Id = "Room-ICTU-001", Title = "Room 1", IsVerified = true },
            new Room { Id = "Room-ICTU-002", Title = "Room 2", IsVerified = true }
        });

        var rooms = await repo.GetByIdsAsync(new[] { "room-ictu-001", "ROOM-ICTU-001", "missing-id", "Room-ICTU-002" });

        Assert.Multiple(() =>
        {
            Assert.That(rooms, Has.Count.EqualTo(2));
            Assert.That(rooms.Select(r => r.Id), Is.EquivalentTo(new[] { "Room-ICTU-001", "Room-ICTU-002" }));
        });
    }

    [Test]
    public async Task CosmosRoomRepository_GetByIdAsync_WhenPointReadReturns404DueToCasing_ShouldFallbackToCaseInsensitiveQueryAndReturnCanonicalRoom()
    {
        var containerMock = new Mock<Container>();
        QueryDefinition? capturedFallbackQuery = null;

        // 1. Point Read with "room-ictu-001" throws 404 NotFound because stored ID is "Room-ICTU-001"
        containerMock
            .Setup(c => c.ReadItemAsync<RoomDocument>(
                "room-ictu-001",
                new PartitionKey("room-ictu-001"),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "activity-1", 1.0));

        // 2. Fallback query with STRINGEQUALS(c.id, @id, true) returns canonical "Room-ICTU-001"
        var returnedDocs = new List<RoomDocument>
        {
            new()
            {
                Id = "Room-ICTU-001",
                Title = "Canonical Cased Room",
                Price = 2000000m,
                Area = 25.0,
                IsVerified = true,
                Status = (int)RoomStatus.Available
            }
        };

        var feedResponseMock = new Mock<FeedResponse<RoomDocument>>();
        feedResponseMock.Setup(r => r.GetEnumerator()).Returns(returnedDocs.GetEnumerator());

        var feedIteratorMock = new Mock<FeedIterator<RoomDocument>>();
        feedIteratorMock.SetupSequence(i => i.HasMoreResults)
            .Returns(true)
            .Returns(false);
        feedIteratorMock.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedResponseMock.Object);

        containerMock
            .Setup(c => c.GetItemQueryIterator<RoomDocument>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
            .Callback<QueryDefinition, string, QueryRequestOptions>((qd, _, _) => capturedFallbackQuery = qd)
            .Returns(feedIteratorMock.Object);

        var cosmosRoomRepo = new CosmosRoomRepository(containerMock.Object);
        var room = await cosmosRoomRepo.GetByIdAsync("  room-ictu-001  ");

        Assert.Multiple(() =>
        {
            Assert.That(capturedFallbackQuery, Is.Not.Null);
            Assert.That(capturedFallbackQuery!.QueryText, Does.Contain("STRINGEQUALS(c.id, @id, true)"));
            Assert.That(room, Is.Not.Null);
            Assert.That(room!.Id, Is.EqualTo("Room-ICTU-001"));
        });
    }

    [Test]
    public async Task CosmosRoomRepository_GetByIdsAsync_ShouldQueryWithArrayContainsAndFallbackToStringEqualsForCaseMismatchedIds()
    {
        var containerMock = new Mock<Container>();
        var capturedQueries = new List<QueryDefinition>();

        // First query (ARRAY_CONTAINS) matches exact-cased "Room-ICTU-001", misses "room-ictu-002" (stored as "Room-ICTU-002")
        var firstBatchDocs = new List<RoomDocument>
        {
            new()
            {
                Id = "Room-ICTU-001",
                Title = "Room 1",
                Price = 1800000m,
                Area = 22.0,
                IsVerified = true,
                Status = (int)RoomStatus.Available,
                ThumbnailUrl = "https://example.com/1.jpg"
            }
        };

        // Second query (STRINGEQUALS fallback) matches "Room-ICTU-002"
        var fallbackBatchDocs = new List<RoomDocument>
        {
            new()
            {
                Id = "Room-ICTU-002",
                Title = "Room 2",
                Price = 2200000m,
                Area = 26.0,
                IsVerified = true,
                Status = (int)RoomStatus.Available
            }
        };

        var firstResponseMock = new Mock<FeedResponse<RoomDocument>>();
        firstResponseMock.Setup(r => r.GetEnumerator()).Returns(() => firstBatchDocs.GetEnumerator());

        var firstIteratorMock = new Mock<FeedIterator<RoomDocument>>();
        firstIteratorMock.SetupSequence(i => i.HasMoreResults).Returns(true).Returns(false);
        firstIteratorMock.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(firstResponseMock.Object);

        var fallbackResponseMock = new Mock<FeedResponse<RoomDocument>>();
        fallbackResponseMock.Setup(r => r.GetEnumerator()).Returns(() => fallbackBatchDocs.GetEnumerator());

        var fallbackIteratorMock = new Mock<FeedIterator<RoomDocument>>();
        fallbackIteratorMock.SetupSequence(i => i.HasMoreResults).Returns(true).Returns(false);
        fallbackIteratorMock.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(fallbackResponseMock.Object);

        containerMock
            .Setup(c => c.GetItemQueryIterator<RoomDocument>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
            .Callback<QueryDefinition, string, QueryRequestOptions>((qd, _, _) => capturedQueries.Add(qd))
            .Returns<QueryDefinition, string, QueryRequestOptions>((qd, _, _) =>
                qd.QueryText.Contains("ARRAY_CONTAINS") ? firstIteratorMock.Object : fallbackIteratorMock.Object);

        var cosmosRoomRepo = new CosmosRoomRepository(containerMock.Object);
        var results = await cosmosRoomRepo.GetByIdsAsync(new[] { "Room-ICTU-001", "room-ictu-002", "ROOM-ICTU-002", "   " });

        Assert.Multiple(() =>
        {
            Assert.That(capturedQueries, Has.Count.EqualTo(2));
            Assert.That(capturedQueries[0].QueryText, Does.Contain("ARRAY_CONTAINS(@ids, c.id)"));
            Assert.That(capturedQueries[1].QueryText, Does.Contain("STRINGEQUALS(c.id, @id0, true)"));
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results.Select(r => r.Id), Is.EquivalentTo(new[] { "Room-ICTU-001", "Room-ICTU-002" }));
        });
    }
}
