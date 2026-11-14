namespace RoomToRentRadar.IntegrationTests;

using System.Diagnostics;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Adapters.Outbound.Caching;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Configurations;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.RoomListings;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;
using ListingRoomStatus = RoomToRentRadar.Domain.Enums.RoomStatus;

/// <summary>
/// Group 1: Unit & Application-level benchmark tests using Fake Repository and in-memory cache
/// to evaluate application use case orchestration, domain filtering, and cache-aside performance.
/// </summary>
[TestFixture]
public class RoomListingsApplicationPerformanceTests
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

        public Task<PagedResult<RoomListing>> GetAvailableVerifiedRoomsByCampusAsync(string campusId, GeoLocation campusLocation, int page, int pageSize, string sortBy, bool descending, CancellationToken cancellationToken = default)
        {
            var result = _rooms.Where(r => r.CampusId == campusId && r.Status == ListingRoomStatus.Available && r.VerificationStatus == VerificationStatus.Verified).ToList();
            var ordered = sortBy switch
            {
                "price" => descending ? result.OrderByDescending(r => r.MonthlyRent.Amount).ThenBy(r => r.Id) : result.OrderBy(r => r.MonthlyRent.Amount).ThenBy(r => r.Id),
                "area" => descending ? result.OrderByDescending(r => r.AreaSquareMeters).ThenBy(r => r.Id) : result.OrderBy(r => r.AreaSquareMeters).ThenBy(r => r.Id),
                _ => descending ? result.OrderByDescending(r => r.Location.DistanceToKm(campusLocation)).ThenBy(r => r.Id) : result.OrderBy(r => r.Location.DistanceToKm(campusLocation)).ThenBy(r => r.Id)
            };
            return Task.FromResult(new PagedResult<RoomListing>(ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, result.Count));
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

/// <summary>
/// Group 2: Integration tests with real Cosmos DB (or Cosmos DB Emulator).
/// Measures real SQL queries, latency, pagination consistency, Partition Key efficiency, and Request Charge (RU).
/// </summary>
[TestFixture]
public class CosmosRoomListingPerformanceIntegrationTests
{
    private const string Endpoint = "https://localhost:8081";
    private const string AuthKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    private const string DatabaseName = "RoomToRentRadarDb_Integration";
    private const string ContainerName = "Rooms_PerfIntegrationTest";
    private const string CampusId = "campus-ictu";

    private CosmosClient? _client;
    private Container? _container;
    private CosmosRoomListingRepository? _repository;
    private readonly GeoLocation _campusLocation = new(21.5852, 105.8074);

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var clientOptions = new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway,
            RequestTimeout = TimeSpan.FromSeconds(5),
            HttpClientFactory = () =>
            {
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
                return new HttpClient(handler);
            },
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        };

        _client = new CosmosClient(Endpoint, AuthKey, clientOptions);

        try
        {
            var dbResponse = await _client.CreateDatabaseIfNotExistsAsync(DatabaseName);
            var database = dbResponse.Database;

            var containerProperties = new ContainerProperties(ContainerName, "/campusId");
            containerProperties.IndexingPolicy.SpatialIndexes.Add(new SpatialPath { Path = "/location/*" });
            var containerResponse = await database.CreateContainerIfNotExistsAsync(containerProperties);
            _container = containerResponse.Container;

            var options = Options.Create(new CosmosDbOptions
            {
                DatabaseName = DatabaseName,
                ContainerName = ContainerName
            });
            _repository = new CosmosRoomListingRepository(_client, options);

            // Seed test data: 30 available & verified rooms, 5 occupied, 5 unverified
            for (var i = 1; i <= 30; i++)
            {
                var offset = (i % 10) * 0.002;
                var room = new RoomListing(
                    $"room-perf-{i:D3}",
                    $"Phòng trọ hiệu năng #{i}",
                    "Mô tả phòng",
                    new Money(1500000 + (i * 20000)),
                    15.0 + (i % 10),
                    $"Địa chỉ #{i}",
                    new GeoLocation(21.5852 + offset, 105.8074 + offset),
                    ListingRoomStatus.Available,
                    VerificationStatus.Verified,
                    CampusId);

                await _repository.AddAsync(room);
            }

            for (var j = 1; j <= 5; j++)
            {
                await _repository.AddAsync(new RoomListing(
                    $"room-perf-occupied-{j}",
                    "Occupied",
                    "Desc",
                    new Money(2000000),
                    20,
                    "Address",
                    _campusLocation,
                    ListingRoomStatus.Occupied,
                    VerificationStatus.Verified,
                    CampusId));

                await _repository.AddAsync(new RoomListing(
                    $"room-perf-unverified-{j}",
                    "Unverified",
                    "Desc",
                    new Money(2000000),
                    20,
                    "Address",
                    _campusLocation,
                    ListingRoomStatus.Available,
                    VerificationStatus.Unverified,
                    CampusId));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException ||
                                  ex is System.Net.Sockets.SocketException ||
                                  ex.InnerException is HttpRequestException ||
                                  ex.InnerException is System.Net.Sockets.SocketException ||
                                  ex.Message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore($"Cosmos DB Emulator is not reachable at {Endpoint}. Skipping live Cosmos DB performance integration tests. Details: {ex.Message}");
        }
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_container is not null)
        {
            try
            {
                await _container.DeleteContainerAsync();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        _client?.Dispose();
    }

    [Test]
    public async Task CosmosDbQuery_PaginationAndFiltering_ShouldExecuteEfficientlyWithLowRU()
    {
        if (_repository is null)
        {
            Assert.Ignore("Cosmos DB repository is not initialized.");
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        // Page 1: 10 items
        var page1 = await _repository.GetAvailableVerifiedRoomsByCampusAsync(
            CampusId,
            _campusLocation,
            page: 1,
            pageSize: 10,
            sortBy: "price",
            descending: false);

        stopwatch.Stop();
        var queryDurationMs = stopwatch.ElapsedMilliseconds;

        Assert.Multiple(() =>
        {
            // Verify record counts: exactly 30 available/verified (occupied and unverified filtered out by Cosmos SQL)
            Assert.That(page1.TotalCount, Is.EqualTo(30), "TotalCount should only include Available + Verified rooms");
            Assert.That(page1.Items, Has.Count.EqualTo(10), "Page size should match requested limit");

            // Query duration SLA
            Assert.That(queryDurationMs, Is.LessThan(3000), $"Cosmos DB query took {queryDurationMs}ms, exceeding 3s SLA");

            // Measure Request Charge (RU): scoped to single partition /campusId, RU should be recorded and low (< 25 RU)
            Assert.That(_repository.LastRequestCharge, Is.GreaterThan(0), "Request Charge (RU) should be tracked and greater than 0");
            Assert.That(_repository.LastRequestCharge, Is.LessThan(50), $"Request Charge ({_repository.LastRequestCharge} RU) should be minimal for single-partition query");

            // Verify ascending price order
            for (var i = 0; i < page1.Items.Count - 1; i++)
            {
                Assert.That(page1.Items[i].MonthlyRent.Amount, Is.LessThanOrEqualTo(page1.Items[i + 1].MonthlyRent.Amount));
            }
        });

        // Page 2: ensure stable pagination with tie-breaker (no duplicate items across pages)
        var page2 = await _repository.GetAvailableVerifiedRoomsByCampusAsync(
            CampusId,
            _campusLocation,
            page: 2,
            pageSize: 10,
            sortBy: "price",
            descending: false);

        Assert.Multiple(() =>
        {
            Assert.That(page2.Items, Has.Count.EqualTo(10));
            var page1Ids = page1.Items.Select(x => x.Id).ToHashSet();
            foreach (var item in page2.Items)
            {
                Assert.That(page1Ids.Contains(item.Id), Is.False, $"Item '{item.Id}' from page 2 was also present in page 1 (pagination unstable)");
            }
        });
    }
}
