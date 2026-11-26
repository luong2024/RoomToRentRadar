namespace RoomToRentRadar.Adapters.Tests;

using System.Diagnostics;
using NUnit.Framework;
using RoomToRentRadar.Adapters.Outbound.Persistence.InMemory;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases;
using RoomToRentRadar.Application.UseCases.RoomListings;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;
using ListingRoomStatus = RoomToRentRadar.Domain.Enums.RoomStatus;
using RoomEntityStatus = RoomToRentRadar.Domain.Entities.RoomStatus;

/// <summary>
/// Task T04.3 - Search Performance Benchmark Suite.
/// Measures response times of GET /api/v1/rooms across 500 listings against AC2 (<= 2s) and US01 AC3 (<= 3s).
/// </summary>
[TestFixture]
public class SearchPerformanceBenchmarkTests
{
    private InMemoryRoomRepository _roomRepository = null!;
    private FakeCampusRepository _campusRepository = null!;
    private FakeCacheService _cacheService = null!;
    private FakeRoomListingRepository _roomListingRepository = null!;
    private SearchRoomsUseCase _searchRoomsUseCase = null!;
    private GetRoomListingsUseCase _getRoomListingsUseCase = null!;

    private readonly Campus _campus = new(
        "campus-ictu",
        "Trường ĐH Công Nghệ Thông Tin và Truyền Thông",
        "ICTU",
        new GeoLocation(21.5852, 105.8073),
        "Đường Z115, Quyết Thắng, Thái Nguyên");

    public const int TotalSeedCount = 500;
    public const int IterationCount = 15;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _roomRepository = new InMemoryRoomRepository();
        _campusRepository = new FakeCampusRepository();
        _cacheService = new FakeCacheService();
        _roomListingRepository = new FakeRoomListingRepository();

        _campusRepository.AddAsync(_campus).GetAwaiter().GetResult();

        _searchRoomsUseCase = new SearchRoomsUseCase(_roomRepository);
        _getRoomListingsUseCase = new GetRoomListingsUseCase(
            _roomListingRepository,
            _campusRepository,
            _cacheService);

        SeedBenchmarkDataset();
    }

    private void SeedBenchmarkDataset()
    {
        var random = new Random(42); // deterministic seed
        var roomTypes = new[] { "private", "shared", "apartment" };
        var availableAmenities = new[] { "wifi", "aircon", "bathroom", "washing", "kitchen", "parking" };

        for (var i = 1; i <= TotalSeedCount; i++)
        {
            var distanceOffsetLat = (random.NextDouble() - 0.5) * 0.15; // ~15 km span
            var distanceOffsetLon = (random.NextDouble() - 0.5) * 0.15;
            var rent = 1_000_000m + (random.Next(0, 35) * 100_000m); // 1M to 4.5M
            var area = 15.0 + random.Next(0, 30); // 15 to 45 m²
            var roomType = roomTypes[random.Next(roomTypes.Length)];
            var amenitiesCount = random.Next(1, 5);
            var roomAmenities = availableAmenities.Take(amenitiesCount).ToList();

            var room = new Room
            {
                Id = $"bench-room-{i:D4}",
                Title = $"Phòng trọ sinh viên ICTU chất lượng cao #{i}",
                Description = $"Phòng trọ đẹp thoáng mát gần cổng trường ICTU số #{i}, an ninh tốt, giờ giấc tự do",
                Price = rent,
                Area = area,
                Address = $"Số {i} Đường Z115, Quyết Thắng, Thái Nguyên",
                Latitude = 21.5852 + distanceOffsetLat,
                Longitude = 105.8073 + distanceOffsetLon,
                Status = RoomEntityStatus.Available,
                IsVerified = true,
                RoomType = roomType,
                Amenities = roomAmenities,
                ThumbnailUrl = $"https://cdn.roomradar.vn/rooms/{i}.jpg",
                CreatedAt = DateTime.UtcNow.AddHours(-i)
            };

            _roomRepository.Upsert(room);
            _roomListingRepository.AddAsync(new RoomListing(
                id: room.Id,
                title: room.Title,
                description: room.Description,
                monthlyRent: new Money(room.Price, "VND"),
                areaSquareMeters: room.Area,
                address: room.Address,
                location: new GeoLocation(room.Latitude ?? 0, room.Longitude ?? 0),
                status: ListingRoomStatus.Available,
                verificationStatus: VerificationStatus.Verified,
                campusId: _campus.Id,
                thumbnailUrl: room.ThumbnailUrl,
                createdAtUtc: room.CreatedAt)).GetAwaiter().GetResult();
        }

        // Add 50 occupied and 50 unverified listings to enforce AC4 filtering workload
        for (var j = 1; j <= 50; j++)
        {
            _roomRepository.Upsert(new Room
            {
                Id = $"bench-occupied-{j}",
                Title = $"Phòng đã thuê #{j}",
                Description = "Đã có người ở",
                Price = 2_000_000m,
                Area = 20.0,
                Latitude = 21.5852,
                Longitude = 105.8073,
                Status = RoomEntityStatus.Rented,
                IsVerified = true
            });

            _roomRepository.Upsert(new Room
            {
                Id = $"bench-unverified-{j}",
                Title = $"Phòng chưa duyệt #{j}",
                Description = "Chờ duyệt",
                Price = 2_000_000m,
                Area = 20.0,
                Latitude = 21.5852,
                Longitude = 105.8073,
                Status = RoomEntityStatus.Available,
                IsVerified = false
            });
        }
    }

    [Test]
    public async Task Benchmark_AllScenarios_MustMeetAC2_Under2000ms()
    {
        // 1. Warm-up phase (10 iterations)
        for (var w = 0; w < 10; w++)
        {
            await _getRoomListingsUseCase.ExecuteAsync(new GetRoomListingsQuery(CampusId: "campus-ictu", Page: 1, PageSize: 20));
        }

        // 2. Define 7 scenarios against original T01.1 (Browse) and T04.1 (Search) use cases
        var scenarios = new (string Name, Func<Task<int>> Action, bool ExpectNonEmpty)[]
        {
            ("1. Default Browse (No Filter)", async () => (await _getRoomListingsUseCase.ExecuteAsync(new GetRoomListingsQuery(
                CampusId: "campus-ictu",
                Page: 1,
                PageSize: 20))).TotalCount, true),

            ("2. Keyword Search", async () => (await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(
                keyword: "ICTU",
                pageNumber: 1,
                pageSize: 20))).TotalCount, true),

            ("3. Price + Area Filter", async () => (await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(
                minPrice: 1_500_000m,
                maxPrice: 3_000_000m,
                minArea: 20.0,
                maxArea: 35.0,
                pageNumber: 1,
                pageSize: 20))).TotalCount, true),

            ("4. Price + Area + Distance Filter", async () => (await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(
                minPrice: 1_500_000m,
                maxPrice: 3_000_000m,
                minArea: 20.0,
                latitude: _campus.Location.Latitude,
                longitude: _campus.Location.Longitude,
                radiusInKm: 5.0,
                pageNumber: 1,
                pageSize: 20))).TotalCount, true),

            ("5. Full Filters", async () => (await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(
                keyword: "ICTU",
                minPrice: 1_200_000m,
                maxPrice: 3_500_000m,
                minArea: 15.0,
                maxArea: 40.0,
                roomType: "private",
                latitude: _campus.Location.Latitude,
                longitude: _campus.Location.Longitude,
                radiusInKm: 10.0,
                amenities: ["wifi", "aircon"],
                pageNumber: 1,
                pageSize: 20))).TotalCount, true),

            ("6. Full Filter + Sort + Pagination", async () => (await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(
                minPrice: 1_200_000m,
                maxPrice: 3_500_000m,
                minArea: 15.0,
                maxArea: 40.0,
                roomType: "private",
                latitude: _campus.Location.Latitude,
                longitude: _campus.Location.Longitude,
                radiusInKm: 10.0,
                amenities: ["wifi", "aircon"],
                sortBy: "price_asc",
                pageNumber: 2,
                pageSize: 15))).TotalCount, true),

            ("7. Zero Results (Strict Boundaries)", async () => (await _searchRoomsUseCase.ExecuteAsync(new SearchRoomsQuery(
                minPrice: 50_000_000m,
                pageNumber: 1,
                pageSize: 20))).TotalCount, false)
        };

        TestContext.Out.WriteLine("==========================================================================================");
        TestContext.Out.WriteLine($"  SEARCH PERFORMANCE BENCHMARK REPORT (Dataset: {TotalSeedCount} listings, {IterationCount} runs)");
        TestContext.Out.WriteLine("==========================================================================================");
        TestContext.Out.WriteLine($"{"Scenario",-38} | {"Min (ms)",-8} | {"Avg (ms)",-8} | {"Max (ms)",-8} | {"P95 (ms)",-8} | {"Status",-8}");
        TestContext.Out.WriteLine("------------------------------------------------------------------------------------------");

        var sw = new Stopwatch();

        foreach (var (name, action, expectNonEmpty) in scenarios)
        {
            var durations = new List<double>(IterationCount);

            for (var i = 0; i < IterationCount; i++)
            {
                sw.Restart();
                var totalCount = await action();
                sw.Stop();
                if (expectNonEmpty)
                {
                    Assert.That(totalCount, Is.GreaterThan(0), $"Scenario '{name}' expected matching results.");
                }
                else
                {
                    Assert.That(totalCount, Is.EqualTo(0), $"Scenario '{name}' expected zero results.");
                }
                durations.Add(sw.Elapsed.TotalMilliseconds);
            }

            durations.Sort();
            var min = durations.First();
            var max = durations.Last();
            var avg = durations.Average();
            var p95Index = (int)Math.Ceiling(durations.Count * 0.95) - 1;
            var p95 = durations[Math.Clamp(p95Index, 0, durations.Count - 1)];

            var passed = p95 <= 2000.0;
            var status = passed ? "PASSED" : "FAILED";

            TestContext.Out.WriteLine($"{name,-38} | {min,8:F2} | {avg,8:F2} | {max,8:F2} | {p95,8:F2} | {status,-8}");

            // AC2: Must be <= 2000 ms (2.0s)
            Assert.That(p95, Is.LessThanOrEqualTo(2000.0), $"Scenario '{name}' exceeded 2000 ms SLA threshold at P95.");
            // AC3: 500 listings browse must be <= 3000 ms
            Assert.That(p95, Is.LessThanOrEqualTo(3000.0));
        }

        TestContext.Out.WriteLine("==========================================================================================");
    }

    #region Helper Fakes

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
            if (value is not null)
            {
                _cache[key] = value;
            }
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _cache.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRoomListingRepository : IRoomListingRepository
    {
        private readonly List<RoomListing> _items = [];

        public Task<PagedResult<RoomListing>> GetAvailableVerifiedRoomsByCampusAsync(
            string campusId, GeoLocation campusLocation, int page, int pageSize, string sortBy, bool descending, CancellationToken cancellationToken = default)
        {
            var results = _items.Where(r => r.CampusId == campusId && r.Status == ListingRoomStatus.Available && r.VerificationStatus == VerificationStatus.Verified);
            results = sortBy switch
            {
                "price" => descending ? results.OrderByDescending(r => r.MonthlyRent.Amount).ThenBy(r => r.Id) : results.OrderBy(r => r.MonthlyRent.Amount).ThenBy(r => r.Id),
                "area" => descending ? results.OrderByDescending(r => r.AreaSquareMeters).ThenBy(r => r.Id) : results.OrderBy(r => r.AreaSquareMeters).ThenBy(r => r.Id),
                _ => descending ? results.OrderByDescending(r => r.Location.ExactDistanceToKm(campusLocation)).ThenBy(r => r.Id) : results.OrderBy(r => r.Location.ExactDistanceToKm(campusLocation)).ThenBy(r => r.Id)
            };
            var list = results.ToList();
            return Task.FromResult(new PagedResult<RoomListing>(list.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, list.Count));
        }

        public Task AddAsync(RoomListing roomListing, CancellationToken cancellationToken = default)
        {
            _items.Add(roomListing);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string roomId, string campusId, CancellationToken cancellationToken = default)
        {
            _items.RemoveAll(item => item.Id == roomId && item.CampusId == campusId);
            return Task.CompletedTask;
        }
    }

    #endregion
}
