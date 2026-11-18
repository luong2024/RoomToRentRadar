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

    [Test]
    public async Task MultiPagePagination_FirstMiddleAndLastPages_ShouldHaveZeroOverlappingAndCorrectCounts()
    {
        // Seed 500 listings
        for (var i = 1; i <= 500; i++)
        {
            var offset = i * 0.0005;
            var room = new RoomListing(
                $"room-scale-{i:D4}",
                $"Phòng trọ #{i}",
                "Mô tả",
                new Money(2000000 + (i * 5000)),
                20.0 + (i % 10),
                "Địa chỉ",
                new GeoLocation(21.5852 + offset, 105.8074 + offset),
                ListingRoomStatus.Available,
                VerificationStatus.Verified,
                "campus-ictu");

            await _repository.AddAsync(room);
        }

        // Query First Page (Page 1), Middle Page (Page 13), and Last Page (Page 25) with PageSize 20
        var firstPage = await _useCase.ExecuteAsync(new GetRoomListingsQuery("campus-ictu", Page: 1, PageSize: 20));
        var middlePage = await _useCase.ExecuteAsync(new GetRoomListingsQuery("campus-ictu", Page: 13, PageSize: 20));
        var lastPage = await _useCase.ExecuteAsync(new GetRoomListingsQuery("campus-ictu", Page: 25, PageSize: 20));

        Assert.Multiple(() =>
        {
            Assert.That(firstPage.TotalCount, Is.EqualTo(500));
            Assert.That(middlePage.TotalCount, Is.EqualTo(500));
            Assert.That(lastPage.TotalCount, Is.EqualTo(500));

            Assert.That(firstPage.Items, Has.Count.EqualTo(20));
            Assert.That(middlePage.Items, Has.Count.EqualTo(20));
            Assert.That(lastPage.Items, Has.Count.EqualTo(20));

            var firstIds = firstPage.Items.Select(x => x.Id).ToHashSet();
            var middleIds = middlePage.Items.Select(x => x.Id).ToHashSet();
            var lastIds = lastPage.Items.Select(x => x.Id).ToHashSet();

            // Zero overlap between first, middle, and last page
            Assert.That(firstIds.Overlaps(middleIds), Is.False, "First page and middle page must not overlap");
            Assert.That(middleIds.Overlaps(lastIds), Is.False, "Middle page and last page must not overlap");
            Assert.That(firstIds.Overlaps(lastIds), Is.False, "First page and last page must not overlap");
        });
    }

    [Test]
    public async Task DistancePagination_WhenRoomsHaveExactSameCoordinates_ConsecutivePagesMustNotDuplicateOrOmitItems()
    {
        // 30 rooms located at exact identical coordinates (e.g. apartment building next to campus)
        var sharedLocation = new GeoLocation(21.5852, 105.8074);
        for (var i = 1; i <= 30; i++)
        {
            var room = new RoomListing(
                $"apt-room-{i:D3}",
                $"Phòng căn hộ #{i}",
                "Cùng tòa nhà",
                new Money(2000000),
                25.0,
                "Tòa nhà A",
                sharedLocation,
                ListingRoomStatus.Available,
                VerificationStatus.Verified,
                "campus-ictu");

            await _repository.AddAsync(room);
        }

        var page1 = await _useCase.ExecuteAsync(new GetRoomListingsQuery("campus-ictu", Page: 1, PageSize: 10, SortBy: "distance"));
        var page2 = await _useCase.ExecuteAsync(new GetRoomListingsQuery("campus-ictu", Page: 2, PageSize: 10, SortBy: "distance"));
        var page3 = await _useCase.ExecuteAsync(new GetRoomListingsQuery("campus-ictu", Page: 3, PageSize: 10, SortBy: "distance"));

        Assert.Multiple(() =>
        {
            Assert.That(page1.Items, Has.Count.EqualTo(10));
            Assert.That(page2.Items, Has.Count.EqualTo(10));
            Assert.That(page3.Items, Has.Count.EqualTo(10));

            var p1Ids = page1.Items.Select(x => x.Id).ToHashSet();
            var p2Ids = page2.Items.Select(x => x.Id).ToHashSet();
            var p3Ids = page3.Items.Select(x => x.Id).ToHashSet();

            Assert.That(p1Ids.Overlaps(p2Ids), Is.False, "Page 1 and Page 2 must not overlap even when distance is identical");
            Assert.That(p2Ids.Overlaps(p3Ids), Is.False, "Page 2 and Page 3 must not overlap even when distance is identical");
            Assert.That(p1Ids.Overlaps(p3Ids), Is.False, "Page 1 and Page 3 must not overlap even when distance is identical");

            var allIds = p1Ids.Union(p2Ids).Union(p3Ids).ToHashSet();
            Assert.That(allIds, Has.Count.EqualTo(30), "All 30 rooms must be uniquely accounted for across 3 pages");
        });
    }

    [Test]
    public async Task DistancePagination_RoomsNearPageBoundary_ShouldMaintainStrictPaginationContinuity()
    {
        // 25 rooms with varying distances spanning page boundary between page 1 and page 2 (pageSize = 10)
        for (var i = 1; i <= 25; i++)
        {
            var offset = i * 0.0005;
            var room = new RoomListing(
                $"boundary-room-{i:D3}",
                $"Boundary Room #{i}",
                "Mô tả",
                new Money(1800000),
                20.0,
                "Địa chỉ",
                new GeoLocation(21.5852 + offset, 105.8074 + offset),
                ListingRoomStatus.Available,
                VerificationStatus.Verified,
                "campus-ictu");

            await _repository.AddAsync(room);
        }

        var page1 = await _useCase.ExecuteAsync(new GetRoomListingsQuery("campus-ictu", Page: 1, PageSize: 10, SortBy: "distance"));
        var page2 = await _useCase.ExecuteAsync(new GetRoomListingsQuery("campus-ictu", Page: 2, PageSize: 10, SortBy: "distance"));
        var page3 = await _useCase.ExecuteAsync(new GetRoomListingsQuery("campus-ictu", Page: 3, PageSize: 10, SortBy: "distance"));

        Assert.Multiple(() =>
        {
            Assert.That(page1.Items, Has.Count.EqualTo(10));
            Assert.That(page2.Items, Has.Count.EqualTo(10));
            Assert.That(page3.Items, Has.Count.EqualTo(5));

            var item10 = page1.Items.Last();
            var item11 = page2.Items.First();

            Assert.That(item10.DistanceToCampusKm, Is.LessThanOrEqualTo(item11.DistanceToCampusKm),
                "Last item of Page 1 must be closer or equal to first item of Page 2");
            Assert.That(item10.Id, Is.Not.EqualTo(item11.Id), "Boundary items across pages must not have duplicate IDs");
        });
    }

    private sealed class FakeRoomListingRepository : IRoomListingRepository
    {
        private readonly List<RoomListing> _rooms = [];

        public Task<PagedResult<RoomListing>> GetAvailableVerifiedRoomsByCampusAsync(
            string campusId,
            GeoLocation campusLocation,
            int page,
            int pageSize,
            string sortBy,
            bool descending,
            CancellationToken cancellationToken = default,
            string? search = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string? roomType = null,
            IReadOnlyList<string>? amenities = null)
        {
            var result = _rooms.Where(r => r.CampusId == campusId && r.Status == ListingRoomStatus.Available && r.VerificationStatus == VerificationStatus.Verified).ToList();
            var ordered = sortBy switch
            {
                "price" => descending ? result.OrderByDescending(r => r.MonthlyRent.Amount).ThenBy(r => r.Id) : result.OrderBy(r => r.MonthlyRent.Amount).ThenBy(r => r.Id),
                "area" => descending ? result.OrderByDescending(r => r.AreaSquareMeters).ThenBy(r => r.Id) : result.OrderBy(r => r.AreaSquareMeters).ThenBy(r => r.Id),
                _ => descending ? result.OrderByDescending(r => r.Location.ExactDistanceToKm(campusLocation)).ThenBy(r => r.Id) : result.OrderBy(r => r.Location.ExactDistanceToKm(campusLocation)).ThenBy(r => r.Id)
            };
            return Task.FromResult(new PagedResult<RoomListing>(ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, result.Count));
        }

        public Task AddAsync(RoomListing roomListing, CancellationToken cancellationToken = default)
        {
            _rooms.Add(roomListing);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string roomId, string campusId, CancellationToken cancellationToken = default)
        {
            _rooms.RemoveAll(room => room.Id == roomId && room.CampusId == campusId);
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

            // Seed test data: 60 available & verified rooms, 10 occupied, 10 unverified (realistic pagination dataset)
            for (var i = 1; i <= 60; i++)
            {
                var offset = (i % 20) * 0.0015;
                var room = new RoomListing(
                    $"room-perf-{i:D3}",
                    $"Phòng trọ hiệu năng #{i}",
                    "Mô tả phòng",
                    new Money(1500000 + (i * 15000)),
                    15.0 + (i % 12),
                    $"Địa chỉ #{i}",
                    new GeoLocation(21.5852 + offset, 105.8074 + offset),
                    ListingRoomStatus.Available,
                    VerificationStatus.Verified,
                    CampusId);

                await _repository.AddAsync(room);
            }

            for (var j = 1; j <= 10; j++)
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
            // Verify record counts: exactly 60 available/verified (occupied and unverified filtered out by Cosmos SQL)
            Assert.That(page1.TotalCount, Is.EqualTo(60), "TotalCount should only include Available + Verified rooms");
            Assert.That(page1.Items, Has.Count.EqualTo(10), "Page size should match requested limit");

            // Query duration SLA (< 3000ms)
            Assert.That(queryDurationMs, Is.LessThan(3000), $"Cosmos DB query took {queryDurationMs}ms, exceeding 3s SLA");

            // Measure Request Charge (RU): scoped to single partition /campusId, RU should be recorded and low (< 50 RU)
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

        // Page 3: multi-page sequential verification
        var page3 = await _repository.GetAvailableVerifiedRoomsByCampusAsync(
            CampusId,
            _campusLocation,
            page: 3,
            pageSize: 10,
            sortBy: "price",
            descending: false);

        Assert.Multiple(() =>
        {
            Assert.That(page2.Items, Has.Count.EqualTo(10));
            Assert.That(page3.Items, Has.Count.EqualTo(10));

            var page1Ids = page1.Items.Select(x => x.Id).ToHashSet();
            var page2Ids = page2.Items.Select(x => x.Id).ToHashSet();
            var page3Ids = page3.Items.Select(x => x.Id).ToHashSet();

            // Total distinct items across 3 pages should be exactly 30 (zero duplicates or omitted items)
            var unionCount = page1Ids.Union(page2Ids).Union(page3Ids).Count();
            Assert.That(unionCount, Is.EqualTo(30), "Pagination must yield completely distinct non-overlapping items across consecutive pages");
        });
    }

    [Test]
    public async Task CosmosDbQuery_DistancePagination_SequentialPagesMustYieldDistinctItems()
    {
        if (_container is null || _repository is null)
        {
            Assert.Ignore($"Cosmos DB Emulator is not reachable at {Endpoint}. Skipping live integration test.");
            return;
        }

        var page1 = await _repository.GetAvailableVerifiedRoomsByCampusAsync(
            CampusId,
            _campusLocation,
            page: 1,
            pageSize: 10,
            sortBy: "distance",
            descending: false);

        var page2 = await _repository.GetAvailableVerifiedRoomsByCampusAsync(
            CampusId,
            _campusLocation,
            page: 2,
            pageSize: 10,
            sortBy: "distance",
            descending: false);

        Assert.Multiple(() =>
        {
            Assert.That(page1.Items, Has.Count.EqualTo(10));
            Assert.That(page2.Items, Has.Count.EqualTo(10));

            var p1Ids = page1.Items.Select(x => x.Id).ToHashSet();
            var p2Ids = page2.Items.Select(x => x.Id).ToHashSet();

            Assert.That(p1Ids.Overlaps(p2Ids), Is.False, "Distance sorted page 1 and page 2 must not have overlapping items");
        });
    }

    [Test]
    public async Task CosmosDbQuery_DistancePagination_RoomsWithIdenticalCoordinatesOnPageBoundary_SequentialPagesMustNotDuplicateOrOmitItems()
    {
        if (_container is null || _repository is null)
        {
            Assert.Ignore($"Cosmos DB Emulator is not reachable at {Endpoint}. Skipping live integration test.");
            return;
        }

        const string boundaryCampusId = "campus-identical-boundary";
        var expectedIds = new HashSet<string>();

        // Explicitly seed 25 rooms with identical coordinates into Cosmos DB across page boundaries
        for (var i = 1; i <= 25; i++)
        {
            var id = $"room-identical-{i:D3}";
            expectedIds.Add(id);
            var room = new RoomListing(
                id,
                $"Phòng cùng tọa độ #{i}",
                "Cùng tọa độ",
                new Money(2000000),
                22.0,
                "Địa chỉ chung",
                _campusLocation, // Exact identical coordinates!
                ListingRoomStatus.Available,
                VerificationStatus.Verified,
                boundaryCampusId);

            await _repository.AddAsync(room);
        }

        // Query Page 1, Page 2, Page 3 (pageSize: 10) on distance sort across the boundary
        var page1 = await _repository.GetAvailableVerifiedRoomsByCampusAsync(
            boundaryCampusId,
            _campusLocation,
            page: 1,
            pageSize: 10,
            sortBy: "distance",
            descending: false);

        var page2 = await _repository.GetAvailableVerifiedRoomsByCampusAsync(
            boundaryCampusId,
            _campusLocation,
            page: 2,
            pageSize: 10,
            sortBy: "distance",
            descending: false);

        var page3 = await _repository.GetAvailableVerifiedRoomsByCampusAsync(
            boundaryCampusId,
            _campusLocation,
            page: 3,
            pageSize: 10,
            sortBy: "distance",
            descending: false);

        Assert.Multiple(() =>
        {
            Assert.That(page1.Items, Has.Count.EqualTo(10));
            Assert.That(page2.Items, Has.Count.EqualTo(10));
            Assert.That(page3.Items, Has.Count.EqualTo(5));

            var p1Ids = page1.Items.Select(x => x.Id).ToHashSet();
            var p2Ids = page2.Items.Select(x => x.Id).ToHashSet();
            var p3Ids = page3.Items.Select(x => x.Id).ToHashSet();

            // 1. Zero duplicates across consecutive pages across boundary
            Assert.That(p1Ids.Overlaps(p2Ids), Is.False, "Page 1 and Page 2 must not have overlapping items");
            Assert.That(p2Ids.Overlaps(p3Ids), Is.False, "Page 2 and Page 3 must not have overlapping items");
            Assert.That(p1Ids.Overlaps(p3Ids), Is.False, "Page 1 and Page 3 must not have overlapping items");

            // 2. Full set reconciliation: 100% of seeded items are present, zero omitted!
            var actualAllIds = p1Ids.Union(p2Ids).Union(p3Ids).ToHashSet();
            Assert.That(actualAllIds, Is.EqualTo(expectedIds), "Sequential pagination across identical coordinates must recover 100% of seeded items without omissions or duplicates");
        });
    }
}
