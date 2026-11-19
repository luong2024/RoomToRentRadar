using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.Favourites;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.Tests.Favourites;

[TestFixture]
public class FavouriteUseCaseTests
{
    private FakeFavouriteRepository _favouriteRepository = null!;
    private FakeRoomRepository _roomRepository = null!;
    private FakeTimeProvider _timeProvider = null!;

    private GetMyFavouritesUseCase _getMyFavouritesUseCase = null!;
    private GetMyFavouriteIdsUseCase _getMyFavouriteIdsUseCase = null!;
    private AddFavouriteUseCase _addFavouriteUseCase = null!;
    private RemoveFavouriteUseCase _removeFavouriteUseCase = null!;
    private SyncFavouritesUseCase _syncFavouritesUseCase = null!;

    [SetUp]
    public void SetUp()
    {
        _favouriteRepository = new FakeFavouriteRepository();
        _roomRepository = new FakeRoomRepository();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero));

        _getMyFavouritesUseCase = new GetMyFavouritesUseCase(_favouriteRepository, _roomRepository);
        _getMyFavouriteIdsUseCase = new GetMyFavouriteIdsUseCase(_favouriteRepository);
        _addFavouriteUseCase = new AddFavouriteUseCase(_favouriteRepository, _roomRepository, _timeProvider);
        _removeFavouriteUseCase = new RemoveFavouriteUseCase(_favouriteRepository);
        _syncFavouritesUseCase = new SyncFavouritesUseCase(_favouriteRepository, _roomRepository, _timeProvider);
    }

    [Test]
    public async Task AddFavourite_WhenRoomIsAvailableAndVerified_ShouldPersistWithCanonicalIdAndReturnAvailableTrue()
    {
        var userId = Guid.NewGuid();
        var room = CreateSampleRoom("Room-ICTU-001", RoomStatus.Available, isVerified: true, thumbnailUrl: "https://example.com/room1.jpg");
        _roomRepository.Seed(room);

        // Pass lowercase input to verify canonical Room.Id is stored
        var result = await _addFavouriteUseCase.ExecuteAsync(userId, "room-ictu-001");

        Assert.Multiple(() =>
        {
            Assert.That(result.RoomId, Is.EqualTo("Room-ICTU-001"));
            Assert.That(result.Id, Is.EqualTo("Room-ICTU-001"));
            Assert.That(result.IsAvailable, Is.True);
            Assert.That(result.IsDeleted, Is.False);
            Assert.That(result.UnavailableMessage, Is.Null);
            Assert.That(result.ThumbnailUrl, Is.EqualTo("https://example.com/room1.jpg"));
            Assert.That(result.FavouritedAtUtc, Is.EqualTo(_timeProvider.GetUtcNow()));
        });

        var stored = await _favouriteRepository.GetByUserAndRoomIdAsync(userId, "ROOM-ICTU-001");
        Assert.That(stored, Is.Not.Null);
        Assert.That(stored!.RoomId, Is.EqualTo("Room-ICTU-001"));
    }

    [Test]
    public async Task AddFavourite_WhenRoomIsAlreadyFavourited_ShouldBeIdempotentAndPreserveOriginalTimestamp()
    {
        var userId = Guid.NewGuid();
        var originalTime = _timeProvider.GetUtcNow();
        var room = CreateSampleRoom("room-ictu-001", RoomStatus.Available, isVerified: true);
        _roomRepository.Seed(room);

        var first = await _addFavouriteUseCase.ExecuteAsync(userId, "room-ictu-001");

        _timeProvider.Advance(TimeSpan.FromMinutes(30));
        var second = await _addFavouriteUseCase.ExecuteAsync(userId, "  ROOM-ICTU-001  ");

        var count = await _favouriteRepository.CountByUserIdAsync(userId);
        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(1));
            Assert.That(second.RoomId, Is.EqualTo(first.RoomId));
            Assert.That(second.FavouritedAtUtc, Is.EqualTo(originalTime));
        });
    }

    [TestCase(RoomStatus.Rented)]
    [TestCase(RoomStatus.Pending)]
    [TestCase(RoomStatus.UnderMaintenance)]
    public async Task AddFavourite_WhenRoomIsVerifiedButNotAvailable_ShouldAllowAddingForUndoAndReturnIsAvailableFalse(RoomStatus status)
    {
        var userId = Guid.NewGuid();
        var room = CreateSampleRoom("room-undo-01", status, isVerified: true);
        _roomRepository.Seed(room);

        var result = await _addFavouriteUseCase.ExecuteAsync(userId, "room-undo-01");

        Assert.Multiple(() =>
        {
            Assert.That(result.RoomId, Is.EqualTo("room-undo-01"));
            Assert.That(result.IsAvailable, Is.False);
            Assert.That(result.Status, Is.EqualTo(status.ToString()));
            Assert.That(result.UnavailableMessage, Is.Not.Null.And.Not.Empty);
        });
    }

    [Test]
    public void AddFavourite_WhenRoomDoesNotExist_ShouldThrowNotFoundException()
    {
        var userId = Guid.NewGuid();

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await _addFavouriteUseCase.ExecuteAsync(userId, "non-existent-room"));
    }

    [Test]
    public void AddFavourite_WhenRoomIsUnverified_ShouldThrowRequestValidationException()
    {
        var userId = Guid.NewGuid();
        _roomRepository.Seed(CreateSampleRoom("room-unverified-01", RoomStatus.Available, isVerified: false));

        var ex = Assert.ThrowsAsync<RequestValidationException>(async () =>
            await _addFavouriteUseCase.ExecuteAsync(userId, "room-unverified-01"));

        Assert.That(ex!.Errors.ContainsKey("roomId"), Is.True);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("room/invalid")]
    [TestCase("room\\invalid")]
    [TestCase("room?invalid")]
    [TestCase("room#invalid")]
    public void AddFavourite_WhenRoomIdIsInvalid_ShouldThrowRequestValidationException(string invalidRoomId)
    {
        var userId = Guid.NewGuid();

        var ex = Assert.ThrowsAsync<RequestValidationException>(async () =>
            await _addFavouriteUseCase.ExecuteAsync(userId, invalidRoomId));

        Assert.That(ex!.Errors.ContainsKey("roomId"), Is.True);
    }

    [Test]
    public async Task AddFavourite_WhenUserReachesMaxFavouritesQuota_ShouldThrowRequestValidationException()
    {
        var userId = Guid.NewGuid();
        for (var i = 1; i <= AddFavouriteUseCase.MaxFavouritesPerUser; i++)
        {
            var id = $"room-{i:D3}";
            _roomRepository.Seed(CreateSampleRoom(id, RoomStatus.Available, isVerified: true));
            await _favouriteRepository.AddIfNotExistsAsync(FavouriteRoom.Create(userId, id, _timeProvider.GetUtcNow()));
        }

        _roomRepository.Seed(CreateSampleRoom("room-overflow", RoomStatus.Available, isVerified: true));

        var ex = Assert.ThrowsAsync<RequestValidationException>(async () =>
            await _addFavouriteUseCase.ExecuteAsync(userId, "room-overflow"));

        Assert.That(ex!.Errors.ContainsKey("favourites"), Is.True);
    }

    [Test]
    public async Task RemoveFavourite_WhenRoomIsNotFavourited_ShouldCompleteIdempotentlyWithoutError()
    {
        var userId = Guid.NewGuid();

        Assert.DoesNotThrowAsync(async () =>
            await _removeFavouriteUseCase.ExecuteAsync(userId, "room-never-favourited"));
    }

    [Test]
    public async Task GetMyFavourites_WhenRoomsBecomeRentedUnverifiedOrDeleted_ShouldReturnFailClosedAvailabilityStates()
    {
        var userId = Guid.NewGuid();

        var availableRoom = CreateSampleRoom("room-1-avail", RoomStatus.Available, isVerified: true);
        var rentedRoom = CreateSampleRoom("room-2-rented", RoomStatus.Rented, isVerified: true);
        var unverifiedRoom = CreateSampleRoom("room-3-unverified", RoomStatus.Available, isVerified: false);

        _roomRepository.Seed(availableRoom);
        _roomRepository.Seed(rentedRoom);
        _roomRepository.Seed(unverifiedRoom);
        // "room-4-deleted" is intentionally NOT seeded in _roomRepository to simulate hard deletion

        await _favouriteRepository.AddIfNotExistsAsync(FavouriteRoom.Create(userId, "room-1-avail", _timeProvider.GetUtcNow().AddMinutes(1)));
        await _favouriteRepository.AddIfNotExistsAsync(FavouriteRoom.Create(userId, "room-2-rented", _timeProvider.GetUtcNow().AddMinutes(2)));
        await _favouriteRepository.AddIfNotExistsAsync(FavouriteRoom.Create(userId, "room-3-unverified", _timeProvider.GetUtcNow().AddMinutes(3)));
        await _favouriteRepository.AddIfNotExistsAsync(FavouriteRoom.Create(userId, "room-4-deleted", _timeProvider.GetUtcNow().AddMinutes(4)));

        var result = await _getMyFavouritesUseCase.ExecuteAsync(userId);

        Assert.That(result.TotalCount, Is.EqualTo(4));
        Assert.Multiple(() =>
        {
            // Ordered by CreatedAtUtc DESC
            Assert.That(result.Items[0].RoomId, Is.EqualTo("room-4-deleted"));
            Assert.That(result.Items[0].IsAvailable, Is.False);
            Assert.That(result.Items[0].IsDeleted, Is.True);
            Assert.That(result.Items[0].Status, Is.EqualTo("Deleted"));
            Assert.That(result.Items[0].UnavailableMessage, Is.Not.Null.And.Not.Empty);

            Assert.That(result.Items[1].RoomId, Is.EqualTo("room-3-unverified"));
            Assert.That(result.Items[1].IsAvailable, Is.False);
            Assert.That(result.Items[1].IsDeleted, Is.False);

            Assert.That(result.Items[2].RoomId, Is.EqualTo("room-2-rented"));
            Assert.That(result.Items[2].IsAvailable, Is.False);
            Assert.That(result.Items[2].Status, Is.EqualTo("Rented"));

            Assert.That(result.Items[3].RoomId, Is.EqualTo("room-1-avail"));
            Assert.That(result.Items[3].IsAvailable, Is.True);
            Assert.That(result.Items[3].UnavailableMessage, Is.Null);
        });
    }

    [Test]
    public async Task SyncFavourites_WhenNullOrEmptyPayload_ShouldReturnExistingFavouritesWithZeroAdded()
    {
        var userId = Guid.NewGuid();
        _roomRepository.Seed(CreateSampleRoom("room-existing", RoomStatus.Available, isVerified: true));
        await _addFavouriteUseCase.ExecuteAsync(userId, "room-existing");

        var nullResult = await _syncFavouritesUseCase.ExecuteAsync(userId, null);
        var emptyResult = await _syncFavouritesUseCase.ExecuteAsync(userId, Array.Empty<string?>());

        Assert.Multiple(() =>
        {
            Assert.That(nullResult.AddedCount, Is.EqualTo(0));
            Assert.That(nullResult.TotalCount, Is.EqualTo(1));
            Assert.That(emptyResult.AddedCount, Is.EqualTo(0));
            Assert.That(emptyResult.TotalCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SyncFavourites_WhenPayloadContainsDuplicatesInvalidAndNonExistentIds_ShouldMergeValidAndSkipInvalid()
    {
        var userId = Guid.NewGuid();
        _roomRepository.Seed(CreateSampleRoom("Room-01", RoomStatus.Available, isVerified: true));
        _roomRepository.Seed(CreateSampleRoom("Room-02", RoomStatus.Rented, isVerified: true));
        _roomRepository.Seed(CreateSampleRoom("Room-Unverified", RoomStatus.Available, isVerified: false));

        var payload = new string?[]
        {
            "room-01",
            "  ROOM-01 ",
            "Room-02",
            "missing-room-99",
            "Room-Unverified",
            "bad/room#id",
            "",
            "   ",
            null
        };

        var result = await _syncFavouritesUseCase.ExecuteAsync(userId, payload);

        Assert.Multiple(() =>
        {
            Assert.That(result.AddedCount, Is.EqualTo(2));
            Assert.That(result.TotalCount, Is.EqualTo(2));
            Assert.That(result.Favourites.Select(f => f.RoomId), Is.EquivalentTo(new[] { "Room-01", "Room-02" }));
            Assert.That(result.SkippedRoomIds, Is.EquivalentTo(new[] { "missing-room-99", "Room-Unverified", "bad/room#id" }));
        });
    }

    [Test]
    public void SyncFavourites_WhenBatchExceedsMaxSyncBatchSize_ShouldThrowRequestValidationException()
    {
        var userId = Guid.NewGuid();
        var oversizedBatch = Enumerable.Range(1, SyncFavouritesUseCase.MaxSyncBatchSize + 1)
            .Select(i => (string?)$"room-{i}")
            .ToList();

        var ex = Assert.ThrowsAsync<RequestValidationException>(async () =>
            await _syncFavouritesUseCase.ExecuteAsync(userId, oversizedBatch));

        Assert.That(ex!.Errors.ContainsKey("roomIds"), Is.True);
    }

    private static Room CreateSampleRoom(
        string id,
        RoomStatus status,
        bool isVerified,
        string? thumbnailUrl = null)
    {
        return new Room
        {
            Id = id,
            Title = $"Room {id}",
            Description = "Sample description",
            Price = 2000000m,
            Deposit = 1000000m,
            Area = 22.5,
            Address = "Z115 Street, Thai Nguyen",
            RoomType = "Private",
            Status = status,
            IsVerified = isVerified,
            ThumbnailUrl = thumbnailUrl,
            Amenities = ["Wifi", "Air conditioner"]
        };
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public FakeTimeProvider(DateTimeOffset initialUtcNow)
        {
            _now = initialUtcNow;
        }

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }

    private sealed class FakeFavouriteRepository : IFavouriteRepository
    {
        private readonly Dictionary<string, FavouriteRoom> _store = new(StringComparer.Ordinal);

        public Task<FavouriteRoom?> GetByUserAndRoomIdAsync(Guid userId, string roomId, CancellationToken cancellationToken = default)
        {
            var id = FavouriteRoom.BuildId(userId, roomId);
            _store.TryGetValue(id, out var fav);
            return Task.FromResult(fav);
        }

        public Task<IReadOnlyList<FavouriteRoom>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var list = _store.Values
                .Where(f => f.UserId == userId)
                .OrderByDescending(f => f.CreatedAtUtc)
                .ThenBy(f => f.RoomId, StringComparer.Ordinal)
                .ToList();

            return Task.FromResult<IReadOnlyList<FavouriteRoom>>(list);
        }

        public Task<int> CountByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var count = _store.Values.Count(f => f.UserId == userId);
            return Task.FromResult(count);
        }

        public Task<(FavouriteRoom Favourite, bool WasCreated)> AddIfNotExistsAsync(FavouriteRoom favourite, CancellationToken cancellationToken = default)
        {
            if (_store.TryGetValue(favourite.Id, out var existing))
            {
                return Task.FromResult((existing, false));
            }

            _store[favourite.Id] = favourite;
            return Task.FromResult((favourite, true));
        }

        public Task RemoveAsync(Guid userId, string roomId, CancellationToken cancellationToken = default)
        {
            var id = FavouriteRoom.BuildId(userId, roomId);
            _store.Remove(id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRoomRepository : IRoomRepository
    {
        private readonly Dictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);

        public void Seed(Room room) => _rooms[room.Id] = room;

        public Task<Room?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            _rooms.TryGetValue(id.Trim(), out var room);
            return Task.FromResult(room);
        }

        public Task<IReadOnlyList<Room>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
        {
            var matched = ids
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => _rooms.TryGetValue(id.Trim(), out var room) ? room : null)
                .Where(r => r is not null)
                .Cast<Room>()
                .ToList();

            return Task.FromResult<IReadOnlyList<Room>>(matched);
        }

        public Task<IReadOnlyList<Room>> GetSimilarRoomsAsync(string roomId, decimal price, double area, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Room>>(Array.Empty<Room>());

        public Task<(IReadOnlyList<Room> Items, int TotalCount)> SearchAsync(SearchRoomsQuery query, CancellationToken ct = default) =>
            Task.FromResult<(IReadOnlyList<Room> Items, int TotalCount)>((Array.Empty<Room>(), 0));
    }
}
