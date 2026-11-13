namespace RoomToRentRadar.IntegrationTests;

using System.Diagnostics;
using RoomToRentRadar.Adapters.Outbound.Caching;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.RoomListings;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;
using Microsoft.Extensions.Caching.Memory;
using ListingRoomStatus = RoomToRentRadar.Domain.Enums.RoomStatus;

[TestFixture]
public class RoomListingsPerformanceAndComplianceTests
{
    private Campus _campus = null!;
    private FakeRoomListingRepository _repository = null!;
    private FakeCampusRepository _campusRepository = null!;
    private MemoryCacheService _cacheService = null!;
    private GetRoomListingsUseCase _useCase = null!;

    [SetUp]
    public void Setup()
    {
        _campus = new Campus(
            "campus-ictu",
            "Trường ĐH Công Nghệ Thông Tin và Truyền Thông",
            "ICTU",
            new GeoLocation(21.5852, 105.8074),
            "Đường Z115, Quyết Thắng, Thái Nguyên");

        _repository = new FakeRoomListingRepository();
        _campusRepository = new FakeCampusRepository();
        _campusRepository.AddAsync(_campus).GetAwaiter().GetResult();

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        _cacheService = new MemoryCacheService(memoryCache);

        _useCase = new GetRoomListingsUseCase(_repository, _campusRepository, _cacheService);
    }

    [Test]
    public async Task AC3_Benchmark_500Listings_ShouldLoadWithin3Seconds()
    {
        // Seed 500 valid listings at varying distances (within ~15 km of ICTU)
        for (var i = 1; i <= 500; i++)
        {
            var offsetLat = (i % 50) * 0.002;
            var offsetLon = (i / 50) * 0.002;
            var room = new RoomListing(
                $"room-{i:D4}",
                $"Phòng trọ sinh viên ICTU số {i}",
                $"Mô tả phòng {i}",
                new Money(1500000 + (i * 10000)),
                18.0 + (i % 15),
                $"Địa chỉ phòng {i}, TP. Thái Nguyên",
                new GeoLocation(21.5852 + offsetLat, 105.8074 + offsetLon),
                ListingRoomStatus.Available,
                VerificationStatus.Verified,
                "campus-ictu",
                thumbnailUrl: $"https://img.roomradar.vn/{i}.jpg");

            await _repository.AddAsync(room);
        }

        // Add 50 occupied and 50 unverified rooms to verify AC4 filtering simultaneously
        for (var j = 1; j <= 50; j++)
        {
            await _repository.AddAsync(new RoomListing(
                $"room-occupied-{j}",
                "Occupied",
                "Desc",
                new Money(2000000),
                20,
                "Address",
                new GeoLocation(21.5852, 105.8074),
                ListingRoomStatus.Occupied,
                VerificationStatus.Verified,
                "campus-ictu"));

            await _repository.AddAsync(new RoomListing(
                $"room-unverified-{j}",
                "Unverified",
                "Desc",
                new Money(2000000),
                20,
                "Address",
                new GeoLocation(21.5852, 105.8074),
                ListingRoomStatus.Available,
                VerificationStatus.Unverified,
                "campus-ictu"));
        }

        var query = new GetRoomListingsQuery("campus-ictu", Page: 1, PageSize: 20);

        // 1. Measure Cold Load (Cache Miss - full computation for 500 items)
        var stopwatch = Stopwatch.StartNew();
        var coldResult = await _useCase.ExecuteAsync(query);
        stopwatch.Stop();
        var coldElapsedMs = stopwatch.ElapsedMilliseconds;

        // 2. Measure Warm Load (Cache Hit)
        stopwatch.Restart();
        var warmResult = await _useCase.ExecuteAsync(query);
        stopwatch.Stop();
        var warmElapsedMs = stopwatch.ElapsedMilliseconds;

        Assert.Multiple(() =>
        {
            // AC3: Must load within 3 seconds (3000 ms) for up to 500 listings
            Assert.That(coldElapsedMs, Is.LessThan(3000), $"Cold load took {coldElapsedMs}ms which exceeds 3000ms");
            Assert.That(warmElapsedMs, Is.LessThan(500), $"Warm load took {warmElapsedMs}ms which exceeds 500ms");

            // AC4: Only 500 Available and Verified listings must be returned (the 100 invalid ones are filtered out)
            Assert.That(coldResult.TotalCount, Is.EqualTo(500));
            Assert.That(coldResult.Items, Has.Count.EqualTo(20));

            // AC1: Sorted by distance by default
            for (var k = 0; k < coldResult.Items.Count - 1; k++)
            {
                Assert.That(coldResult.Items[k].DistanceToCampusKm, Is.LessThanOrEqualTo(coldResult.Items[k + 1].DistanceToCampusKm));
            }

            // AC2: Card properties present
            var firstCard = coldResult.Items[0];
            Assert.That(firstCard.Title, Is.Not.Null.And.Not.Empty);
            Assert.That(firstCard.MonthlyRent, Is.GreaterThan(0));
            Assert.That(firstCard.AreaSquareMeters, Is.GreaterThan(0));
            Assert.That(firstCard.DistanceToCampusKm, Is.GreaterThanOrEqualTo(0));
            Assert.That(firstCard.IsVerified, Is.True);
        });
    }

    private sealed class FakeRoomListingRepository : IRoomListingRepository
    {
        private readonly List<RoomListing> _rooms = [];

        public Task<IReadOnlyList<RoomListing>> GetAvailableVerifiedRoomsByCampusAsync(string campusId, CancellationToken cancellationToken = default)
        {
            var result = _rooms.Where(r => r.CampusId == campusId && r.Status == ListingRoomStatus.Available && r.VerificationStatus == VerificationStatus.Verified).ToList();
            return Task.FromResult<IReadOnlyList<RoomListing>>(result);
        }

        public Task AddAsync(RoomListing roomListing, CancellationToken cancellationToken = default)
        {
            _rooms.Add(roomListing);
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
}
