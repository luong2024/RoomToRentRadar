namespace RoomToRentRadar.Application.Tests;

using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.RoomListings;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;

[TestFixture]
public class GetRoomListingsUseCaseTests
{
    private FakeRoomListingRepository _roomRepository = null!;
    private FakeCampusRepository _campusRepository = null!;
    private FakeCacheService _cacheService = null!;
    private GetRoomListingsUseCase _useCase = null!;

    private readonly Campus _ictuCampus = new(
        "campus-ictu",
        "Trường ĐH Công Nghệ Thông Tin và Truyền Thông",
        "ICTU",
        new GeoLocation(21.5852, 105.8074),
        "Đường Z115, Quyết Thắng, Thái Nguyên");

    [SetUp]
    public void Setup()
    {
        _roomRepository = new FakeRoomListingRepository();
        _campusRepository = new FakeCampusRepository();
        _cacheService = new FakeCacheService();

        _campusRepository.AddAsync(_ictuCampus).GetAwaiter().GetResult();

        _useCase = new GetRoomListingsUseCase(_roomRepository, _campusRepository, _cacheService);
    }

    [TestCase("")]
    [TestCase("   ")]
    public void ExecuteAsync_WhenCampusIdIsEmpty_ShouldThrowValidationException(string campusId)
    {
        var query = new GetRoomListingsQuery(campusId);

        Assert.ThrowsAsync<ValidationException>(() => _useCase.ExecuteAsync(query));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void ExecuteAsync_WhenPageIsInvalid_ShouldThrowValidationException(int page)
    {
        var query = new GetRoomListingsQuery("campus-ictu", Page: page);

        Assert.ThrowsAsync<ValidationException>(() => _useCase.ExecuteAsync(query));
    }

    [TestCase(0)]
    [TestCase(101)]
    public void ExecuteAsync_WhenPageSizeIsOutOfRange_ShouldThrowValidationException(int pageSize)
    {
        var query = new GetRoomListingsQuery("campus-ictu", PageSize: pageSize);

        Assert.ThrowsAsync<ValidationException>(() => _useCase.ExecuteAsync(query));
    }

    [Test]
    public void ExecuteAsync_WhenCampusNotFound_ShouldThrowCampusNotFoundException()
    {
        var query = new GetRoomListingsQuery("non-existent-campus");

        Assert.ThrowsAsync<CampusNotFoundException>(() => _useCase.ExecuteAsync(query));
    }

    [Test]
    public async Task ExecuteAsync_WhenCacheHit_ShouldReturnCachedResultWithoutCallingRepository()
    {
        var cachedItems = new List<RoomListingDto>
        {
            new("room-cached", "Cached Room", 1500000, "VND", 20, 0.5, true, "123 Street", null, "campus-ictu")
        };
        var cachedResult = new PagedResult<RoomListingDto>(cachedItems, 1, 20, 1);

        var cacheKey = "rooms:campus:campus-ictu:p1:s20:sortdistance_asc";
        await _cacheService.SetAsync(cacheKey, cachedResult);

        var query = new GetRoomListingsQuery("campus-ictu");
        var result = await _useCase.ExecuteAsync(query);

        Assert.Multiple(() =>
        {
            Assert.That(result.Items, Has.Count.EqualTo(1));
            Assert.That(result.Items[0].Id, Is.EqualTo("room-cached"));
            Assert.That(_roomRepository.GetCallCount, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task ExecuteAsync_AC1_ShouldSortByDistanceAscendingByDefault()
    {
        // Near room (~0.1 km)
        var nearRoom = CreateListing("room-near", new GeoLocation(21.5860, 105.8074), RoomStatus.Available, VerificationStatus.Verified);
        // Far room (~10 km)
        var farRoom = CreateListing("room-far", new GeoLocation(21.6800, 105.8074), RoomStatus.Available, VerificationStatus.Verified);
        // Mid room (~2 km)
        var midRoom = CreateListing("room-mid", new GeoLocation(21.6000, 105.8074), RoomStatus.Available, VerificationStatus.Verified);

        await _roomRepository.AddAsync(farRoom);
        await _roomRepository.AddAsync(nearRoom);
        await _roomRepository.AddAsync(midRoom);

        var query = new GetRoomListingsQuery("campus-ictu");
        var result = await _useCase.ExecuteAsync(query);

        Assert.Multiple(() =>
        {
            Assert.That(result.Items, Has.Count.EqualTo(3));
            Assert.That(result.Items[0].Id, Is.EqualTo("room-near"));
            Assert.That(result.Items[1].Id, Is.EqualTo("room-mid"));
            Assert.That(result.Items[2].Id, Is.EqualTo("room-far"));
            Assert.That(result.Items[0].DistanceToCampusKm, Is.LessThan(result.Items[1].DistanceToCampusKm));
            Assert.That(result.Items[1].DistanceToCampusKm, Is.LessThan(result.Items[2].DistanceToCampusKm));
        });
    }

    [Test]
    public async Task ExecuteAsync_AC4_ShouldFilterOutUnverifiedOrUnavailableListings()
    {
        var validRoom = CreateListing("room-valid", new GeoLocation(21.5860, 105.8074), RoomStatus.Available, VerificationStatus.Verified);
        var occupiedRoom = CreateListing("room-occupied", new GeoLocation(21.5860, 105.8074), RoomStatus.Occupied, VerificationStatus.Verified);
        var unverifiedRoom = CreateListing("room-unverified", new GeoLocation(21.5860, 105.8074), RoomStatus.Available, VerificationStatus.Unverified);
        var pendingRoom = CreateListing("room-pending", new GeoLocation(21.5860, 105.8074), RoomStatus.Available, VerificationStatus.Pending);

        await _roomRepository.AddAsync(validRoom);
        await _roomRepository.AddAsync(occupiedRoom);
        await _roomRepository.AddAsync(unverifiedRoom);
        await _roomRepository.AddAsync(pendingRoom);

        var query = new GetRoomListingsQuery("campus-ictu");
        var result = await _useCase.ExecuteAsync(query);

        Assert.Multiple(() =>
        {
            Assert.That(result.Items, Has.Count.EqualTo(1));
            Assert.That(result.Items[0].Id, Is.EqualTo("room-valid"));
            Assert.That(result.TotalCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ExecuteAsync_Pagination_ShouldReturnCorrectPageAndTotalCount()
    {
        for (var i = 1; i <= 25; i++)
        {
            var room = CreateListing($"room-{i:D2}", new GeoLocation(21.5852 + (i * 0.001), 105.8074), RoomStatus.Available, VerificationStatus.Verified);
            await _roomRepository.AddAsync(room);
        }

        var page1Query = new GetRoomListingsQuery("campus-ictu", Page: 1, PageSize: 10);
        var page1Result = await _useCase.ExecuteAsync(page1Query);

        var page2Query = new GetRoomListingsQuery("campus-ictu", Page: 2, PageSize: 10);
        var page2Result = await _useCase.ExecuteAsync(page2Query);

        Assert.Multiple(() =>
        {
            Assert.That(page1Result.TotalCount, Is.EqualTo(25));
            Assert.That(page1Result.Items, Has.Count.EqualTo(10));
            Assert.That(page1Result.Page, Is.EqualTo(1));

            Assert.That(page2Result.TotalCount, Is.EqualTo(25));
            Assert.That(page2Result.Items, Has.Count.EqualTo(10));
            Assert.That(page2Result.Page, Is.EqualTo(2));
            Assert.That(page2Result.Items[0].Id, Is.Not.EqualTo(page1Result.Items[0].Id));
        });
    }

    private static RoomListing CreateListing(string id, GeoLocation location, RoomStatus status, VerificationStatus verification)
    {
        return new RoomListing(
            id,
            $"Room {id}",
            "Description",
            new Money(2000000),
            25.0,
            "Address",
            location,
            status,
            verification,
            "campus-ictu");
    }

    private sealed class FakeRoomListingRepository : IRoomListingRepository
    {
        private readonly List<RoomListing> _items = [];
        public int GetCallCount { get; private set; }

        public Task<IReadOnlyList<RoomListing>> GetAvailableVerifiedRoomsByCampusAsync(string campusId, CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            var results = _items.Where(r => r.CampusId == campusId && r.Status == RoomStatus.Available && r.VerificationStatus == VerificationStatus.Verified).ToList();
            return Task.FromResult<IReadOnlyList<RoomListing>>(results);
        }

        public Task AddAsync(RoomListing roomListing, CancellationToken cancellationToken = default)
        {
            _items.Add(roomListing);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCampusRepository : ICampusRepository
    {
        private readonly Dictionary<string, Campus> _campuses = [];

        public Task<Campus?> GetByIdAsync(string campusId, CancellationToken cancellationToken = default)
        {
            _campuses.TryGetValue(campusId, out var campus);
            return Task.FromResult(campus);
        }

        public Task AddAsync(Campus campus, CancellationToken cancellationToken = default)
        {
            _campuses[campus.Id] = campus;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _cache = [];

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue(key, out var val) && val is T typed)
            {
                return Task.FromResult<T?>(typed);
            }
            return Task.FromResult<T?>(default);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            _cache[key] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _cache.Remove(key);
            return Task.CompletedTask;
        }
    }
}
