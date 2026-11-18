using System.Net;
using Microsoft.Azure.Cosmos;
using Moq;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Context;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Adapters.Outbound.Persistence.InMemory;
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
    public async Task CosmosRoomRepository_GetByIdsAsync_ShouldQueryWithArrayContainsAndReturnMappedRooms()
    {
        var containerMock = new Mock<Container>();
        QueryDefinition? capturedQuery = null;

        var returnedDocs = new List<RoomDocument>
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
            .Callback<QueryDefinition, string, QueryRequestOptions>((qd, _, _) => capturedQuery = qd)
            .Returns(feedIteratorMock.Object);

        var cosmosRoomRepo = new CosmosRoomRepository(containerMock.Object);
        var results = await cosmosRoomRepo.GetByIdsAsync(new[] { "Room-ICTU-001", "Room-ICTU-001", "   " });

        Assert.Multiple(() =>
        {
            Assert.That(capturedQuery, Is.Not.Null);
            Assert.That(capturedQuery!.QueryText, Does.Contain("ARRAY_CONTAINS(@ids, c.id)"));
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Id, Is.EqualTo("Room-ICTU-001"));
            Assert.That(results[0].ThumbnailUrl, Is.EqualTo("https://example.com/1.jpg"));
        });
    }
}
