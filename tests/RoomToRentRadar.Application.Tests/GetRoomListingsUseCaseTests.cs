namespace RoomToRentRadar.Application.Tests;

using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.RoomListings;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;
using ListingRoomStatus = RoomToRentRadar.Domain.Enums.RoomStatus;

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

    [TestCase("invalid")]
    [TestCase("name")]
    [TestCase("created_at")]
    [TestCase("123")]
    public void ExecuteAsync_WhenSortByIsInvalid_ShouldThrowValidationException(string sortBy)
    {
        var query = new GetRoomListingsQuery("campus-ictu", SortBy: sortBy);

        var ex = Assert.ThrowsAsync<ValidationException>(() => _useCase.ExecuteAsync(query));
        Assert.That(ex!.Errors.ContainsKey("SortBy"), Is.True);
    }

    [TestCase("invalid")]
    [TestCase("descending")]
    [TestCase("ascending")]
    [TestCase("up")]
    public void ExecuteAsync_WhenSortOrderIsInvalid_ShouldThrowValidationException(string sortOrder)
    {
        var query = new GetRoomListingsQuery("campus-ictu", SortOrder: sortOrder);

        var ex = Assert.ThrowsAsync<ValidationException>(() => _useCase.ExecuteAsync(query));
        Assert.That(ex!.Errors.ContainsKey("SortOrder"), Is.True);
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
        var nearRoom = CreateListing("room-near", new GeoLocation(21.5860, 105.8074), ListingRoomStatus.Available, VerificationStatus.Verified);
        // Far room (~10 km)
        var farRoom = CreateListing("room-far", new GeoLocation(21.6800, 105.8074), ListingRoomStatus.Available, VerificationStatus.Verified);
        // Mid room (~2 km)
        var midRoom = CreateListing("room-mid", new GeoLocation(21.6000, 105.8074), ListingRoomStatus.Available, VerificationStatus.Verified);

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
        var validRoom = CreateListing("room-valid", new GeoLocation(21.5860, 105.8074), ListingRoomStatus.Available, VerificationStatus.Verified);
        var occupiedRoom = CreateListing("room-occupied", new GeoLocation(21.5860, 105.8074), ListingRoomStatus.Occupied, VerificationStatus.Verified);
        var unverifiedRoom = CreateListing("room-unverified", new GeoLocation(21.5860, 105.8074), ListingRoomStatus.Available, VerificationStatus.Unverified);
        var pendingRoom = CreateListing("room-pending", new GeoLocation(21.5860, 105.8074), ListingRoomStatus.Available, VerificationStatus.Pending);

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
            var room = CreateListing($"room-{i:D2}", new GeoLocation(21.5852 + (i * 0.001), 105.8074), ListingRoomStatus.Available, VerificationStatus.Verified);
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

    [Test]
    public async Task ExecuteAsync_SortByPriceAndArea_ShouldSortCorrectly()
    {
        var cheapSmall = new RoomListing("r1", "Cheap Small", "Desc", new Money(1000000), 15.0, "Addr", new GeoLocation(21.5852, 105.8074), ListingRoomStatus.Available, VerificationStatus.Verified, "campus-ictu");
        var expensiveBig = new RoomListing("r2", "Expensive Big", "Desc", new Money(3000000), 35.0, "Addr", new GeoLocation(21.5852, 105.8074), ListingRoomStatus.Available, VerificationStatus.Verified, "campus-ictu");

        await _roomRepository.AddAsync(expensiveBig);
        await _roomRepository.AddAsync(cheapSmall);

        var priceAsc = await _useCase.ExecuteAsync(new GetRoomListingsQuery("campus-ictu", SortBy: "price", SortOrder: "asc"));
        Assert.That(priceAsc.Items[0].Id, Is.EqualTo("r1"));

        var priceDesc = await _useCase.ExecuteAsync(new GetRoomListingsQuery("campus-ictu", SortBy: "price", SortOrder: "desc"));
        Assert.That(priceDesc.Items[0].Id, Is.EqualTo("r2"));

        var areaAsc = await _useCase.ExecuteAsync(new GetRoomListingsQuery("campus-ictu", SortBy: "area", SortOrder: "asc"));
        Assert.That(areaAsc.Items[0].Id, Is.EqualTo("r1"));
    }

    [Test]
    public async Task ExecuteAsync_WhenValuesEqual_TieBreakerEnsuresStablePaginationOrder()
    {
        // 3 rooms with the exact same price
        var roomA = new RoomListing("room-A", "Room A", "Desc", new Money(2000000), 20.0, "Addr", new GeoLocation(21.5852, 105.8074), ListingRoomStatus.Available, VerificationStatus.Verified, "campus-ictu");
        var roomB = new RoomListing("room-B", "Room B", "Desc", new Money(2000000), 20.0, "Addr", new GeoLocation(21.5852, 105.8074), ListingRoomStatus.Available, VerificationStatus.Verified, "campus-ictu");
        var roomC = new RoomListing("room-C", "Room C", "Desc", new Money(2000000), 20.0, "Addr", new GeoLocation(21.5852, 105.8074), ListingRoomStatus.Available, VerificationStatus.Verified, "campus-ictu");

        await _roomRepository.AddAsync(roomC);
        await _roomRepository.AddAsync(roomA);
        await _roomRepository.AddAsync(roomB);

        var result = await _useCase.ExecuteAsync(new GetRoomListingsQuery("campus-ictu", SortBy: "price", SortOrder: "asc"));

        // Tie-breaker by Id guarantees deterministic ascending order: room-A, room-B, room-C
        Assert.Multiple(() =>
        {
            Assert.That(result.Items[0].Id, Is.EqualTo("room-A"));
            Assert.That(result.Items[1].Id, Is.EqualTo("room-B"));
            Assert.That(result.Items[2].Id, Is.EqualTo("room-C"));
        });
    }

    private static RoomListing CreateListing(string id, GeoLocation location, ListingRoomStatus status, VerificationStatus verification)
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

        public Task<PagedResult<RoomListing>> GetAvailableVerifiedRoomsByCampusAsync(string campusId, GeoLocation campusLocation, int page, int pageSize, string sortBy, bool descending, CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            var results = _items.Where(r => r.CampusId == campusId && r.Status == ListingRoomStatus.Available && r.VerificationStatus == VerificationStatus.Verified);
            results = sortBy switch
            {
                "price" => descending ? results.OrderByDescending(r => r.MonthlyRent.Amount).ThenBy(r => r.Id) : results.OrderBy(r => r.MonthlyRent.Amount).ThenBy(r => r.Id),
                "area" => descending ? results.OrderByDescending(r => r.AreaSquareMeters).ThenBy(r => r.Id) : results.OrderBy(r => r.AreaSquareMeters).ThenBy(r => r.Id),
                _ => descending ? results.OrderByDescending(r => r.Location.DistanceToKm(campusLocation)).ThenBy(r => r.Id) : results.OrderBy(r => r.Location.DistanceToKm(campusLocation)).ThenBy(r => r.Id)
            };
            var materialized = results.ToList();
            return Task.FromResult(new PagedResult<RoomListing>(materialized.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, materialized.Count));
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
