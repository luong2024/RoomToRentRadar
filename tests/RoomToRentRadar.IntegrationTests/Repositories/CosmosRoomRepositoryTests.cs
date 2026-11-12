using System.Net;
using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.IntegrationTests.Repositories;

[TestFixture]
public class CosmosRoomRepositoryTests
{
    private const string Endpoint = "https://localhost:8081";
    private const string AuthKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    private const string DatabaseName = "RoomToRentRadarDb_Integration";
    private const string ContainerName = "Rooms_Search_Test";

    private CosmosClient? _client;
    private Database? _database;
    private Container? _container;
    private CosmosRoomRepository? _repository;
    private string _batchId = null!;

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
        _batchId = Guid.NewGuid().ToString("N")[..8];

        try
        {
            var dbResponse = await _client.CreateDatabaseIfNotExistsAsync(DatabaseName);
            _database = dbResponse.Database;

            var containerProperties = new ContainerProperties(ContainerName, "/id");
            containerProperties.IndexingPolicy.SpatialIndexes.Add(new SpatialPath
            {
                Path = "/location/*",
                SpatialTypes = { SpatialType.Point }
            });
            containerProperties.IndexingPolicy.SpatialIndexes.Add(new SpatialPath
            {
                Path = "/Location/*",
                SpatialTypes = { SpatialType.Point }
            });

            var containerResponse = await _database.CreateContainerIfNotExistsAsync(containerProperties);
            _container = containerResponse.Container;

            _repository = new CosmosRoomRepository(_container);

            // Seed test rooms
            await SeedRoomsAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException ||
                                  ex is System.Net.Sockets.SocketException ||
                                  ex.InnerException is HttpRequestException ||
                                  ex.InnerException is System.Net.Sockets.SocketException ||
                                  ex.Message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase) ||
                                  ex.Message.Contains("No connection could be made", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore($"Cosmos DB Emulator is not reachable at {Endpoint}. Skipping live integration tests on environments without running emulator. Details: {ex.Message}");
        }
    }

    private async Task SeedRoomsAsync()
    {
        if (_container is null) return;

        var rooms = new[]
        {
            new Room
            {
                Id = $"room-near-{_batchId}",
                Title = $"Phòng gần trung tâm {_batchId}",
                Description = "Gần trường và bến xe",
                Price = 2500000m,
                Area = 25.0,
                RoomType = "Khép kín",
                Address = "Đường Lương Ngọc Quyến, Thái Nguyên",
                Latitude = 21.5890,
                Longitude = 105.8100,
                Status = RoomStatus.Available,
                IsVerified = true
            },
            new Room
            {
                Id = $"room-far-{_batchId}",
                Title = $"Phòng xa ngoại ô {_batchId}",
                Description = "Yên tĩnh",
                Price = 2800000m,
                Area = 30.0,
                RoomType = "Khép kín",
                Address = "Khu ngoại ô, Thái Nguyên",
                Latitude = 21.7500,
                Longitude = 105.9500,
                Status = RoomStatus.Available,
                IsVerified = true
            },
            new Room
            {
                Id = $"room-expensive-{_batchId}",
                Title = $"Căn hộ cao cấp {_batchId}",
                Description = "Full tiện nghi",
                Price = 6000000m,
                Area = 55.0,
                RoomType = "Căn hộ",
                Address = "Khu đô thị, Thái Nguyên",
                Latitude = 21.5850,
                Longitude = 105.8050,
                Status = RoomStatus.Available,
                IsVerified = true
            },
            new Room
            {
                Id = $"room-unverified-{_batchId}",
                Title = $"Phòng chưa duyệt {_batchId}",
                Description = "Giá rẻ",
                Price = 2400000m,
                Area = 24.0,
                RoomType = "Khép kín",
                Address = "Đường Z115, Thái Nguyên",
                Latitude = 21.5880,
                Longitude = 105.8090,
                Status = RoomStatus.Available,
                IsVerified = false
            }
        };

        foreach (var room in rooms)
        {
            var doc = RoomDocument.FromDomain(room);
            await _container.UpsertItemAsync(doc, new PartitionKey(room.Id));
        }
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_database is not null)
        {
            try
            {
                await _database.DeleteAsync();
            }
            catch
            {
                // Silently ignore cleanup errors
            }
        }

        _client?.Dispose();
    }

    // ==========================================
    // TESTS TỪ TASK T02.1 (Basic CRUD & Similar)
    // ==========================================
    [Test]
    public async Task AddRoom_DirectToCosmosEmulator_ShouldSucceed()
    {
        if (_repository is null) Assert.Ignore("Cosmos DB repository is not initialized.");

        // Arrange
        var room = new Room
        {
            Id = $"room-add-{Guid.NewGuid()}",
            Title = "Phòng trọ cao cấp gần ĐH Thái Nguyên",
            Description = "Full nội thất, ban công, an ninh tốt",
            Price = 2800000m,
            Area = 28.5,
            Address = "Đường Z115, TP Thái Nguyên",
            LandlordPhone = "0987654321",
            Status = RoomStatus.Available,
            IsVerified = true,
            RoomType = "Khép kín"
        };

        // Act
        var created = await _repository!.CreateAsync(room);

        // Assert
        Assert.That(created, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(created.Id, Is.EqualTo(room.Id));
            Assert.That(created.Title, Is.EqualTo(room.Title));
            Assert.That(created.Price, Is.EqualTo(room.Price));
            Assert.That(created.Area, Is.EqualTo(room.Area));
        });
    }

    [Test]
    public async Task GetByIdAsync_WhenRoomExists_ShouldReturnRoomSuccessfully()
    {
        if (_repository is null) Assert.Ignore("Cosmos DB repository is not initialized.");

        // Arrange
        var room = new Room
        {
            Id = $"room-get-{Guid.NewGuid()}",
            Title = "Phòng trọ giá rẻ cho sinh viên",
            Description = "Gần bến xe, tiện xe bus",
            Price = 1500000m,
            Area = 20.0,
            Address = "Đường Lương Ngọc Quyến, TP Thái Nguyên",
            LandlordPhone = "0912345678",
            Status = RoomStatus.Available,
            IsVerified = true,
            RoomType = "Ở ghép"
        };
        await _repository!.CreateAsync(room);

        // Act
        var loaded = await _repository.GetByIdAsync(room.Id);

        // Assert
        Assert.That(loaded, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Id, Is.EqualTo(room.Id));
            Assert.That(loaded.Title, Is.EqualTo(room.Title));
            Assert.That(loaded.Price, Is.EqualTo(room.Price));
            Assert.That(loaded.Area, Is.EqualTo(room.Area));
            Assert.That(loaded.Status, Is.EqualTo(RoomStatus.Available));
        });
    }

    [Test]
    public async Task GetSimilarRoomsAsync_ShouldReturnRoomsMatchingBothPriceAndAreaConditions()
    {
        if (_repository is null) Assert.Ignore("Cosmos DB repository is not initialized.");

        var testBatchId = Guid.NewGuid().ToString("N")[..8];
        var targetRoomId = $"target-{testBatchId}";

        var targetRoom = new Room
        {
            Id = targetRoomId,
            Title = "Phòng mục tiêu (đã thuê)",
            Price = 3000000m,
            Area = 30.0,
            Status = RoomStatus.Rented
        };

        var matchingRoom1 = new Room { Id = $"match1-{testBatchId}", Title = "Phòng tương tự chuẩn 1", Price = 3100000m, Area = 31.0, Status = RoomStatus.Available };
        var matchingRoom2 = new Room { Id = $"match2-{testBatchId}", Title = "Phòng tương tự chuẩn 2", Price = 2500000m, Area = 26.0, Status = RoomStatus.Available };
        var areaTooSmallRoom = new Room { Id = $"small-area-{testBatchId}", Title = "Phòng diện tích quá nhỏ", Price = 3000000m, Area = 18.0, Status = RoomStatus.Available };
        var areaTooLargeRoom = new Room { Id = $"large-area-{testBatchId}", Title = "Phòng diện tích quá lớn", Price = 3000000m, Area = 45.0, Status = RoomStatus.Available };
        var priceTooCheapRoom = new Room { Id = $"cheap-price-{testBatchId}", Title = "Phòng giá quá rẻ", Price = 1800000m, Area = 30.0, Status = RoomStatus.Available };
        var priceTooExpensiveRoom = new Room { Id = $"expensive-price-{testBatchId}", Title = "Phòng giá quá đắt", Price = 4500000m, Area = 30.0, Status = RoomStatus.Available };
        var nonAvailableRoom = new Room { Id = $"rented-{testBatchId}", Title = "Phòng đã cho thuê", Price = 3000000m, Area = 30.0, Status = RoomStatus.Rented };

        await _repository!.CreateAsync(targetRoom);
        await _repository.CreateAsync(matchingRoom1);
        await _repository.CreateAsync(matchingRoom2);
        await _repository.CreateAsync(areaTooSmallRoom);
        await _repository.CreateAsync(areaTooLargeRoom);
        await _repository.CreateAsync(priceTooCheapRoom);
        await _repository.CreateAsync(priceTooExpensiveRoom);
        await _repository.CreateAsync(nonAvailableRoom);

        var similarRooms = await _repository.GetSimilarRoomsAsync(targetRoomId, price: 3000000m, area: 30.0);

        Assert.That(similarRooms, Is.Not.Null);
        var similarIds = similarRooms.Select(r => r.Id).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(similarIds, Does.Contain(matchingRoom1.Id));
            Assert.That(similarIds, Does.Contain(matchingRoom2.Id));
            Assert.That(similarIds, Does.Not.Contain(targetRoom.Id));
            Assert.That(similarIds, Does.Not.Contain(areaTooSmallRoom.Id));
            Assert.That(similarIds, Does.Not.Contain(areaTooLargeRoom.Id));
            Assert.That(similarIds, Does.Not.Contain(priceTooCheapRoom.Id));
            Assert.That(similarIds, Does.Not.Contain(priceTooExpensiveRoom.Id));
            Assert.That(similarIds, Does.Not.Contain(nonAvailableRoom.Id));
        });
    }

    // ==========================================
    // TESTS TỪ TASK T04.1 (Search & GPS)
    // ==========================================
    [Test]
    public async Task SearchAsync_ShouldReturnRooms_WhenMatchingPriceAndArea()
    {
        if (_repository is null) Assert.Ignore("Cosmos DB repository is not initialized.");

        var query = new SearchRoomsQuery(
            minPrice: 2000000m,
            maxPrice: 3000000m,
            minArea: 20.0,
            maxArea: 35.0,
            keyword: _batchId);

        var (items, totalCount) = await _repository!.SearchAsync(query);

        Assert.That(totalCount, Is.GreaterThanOrEqualTo(2));
        var itemIds = items.Select(r => r.Id).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(itemIds, Does.Contain($"room-near-{_batchId}"));
            Assert.That(itemIds, Does.Contain($"room-far-{_batchId}"));
            Assert.That(itemIds, Does.Not.Contain($"room-expensive-{_batchId}"));
            Assert.That(itemIds, Does.Not.Contain($"room-unverified-{_batchId}"));
        });
    }

    [Test]
    public async Task SearchAsync_WithGPSLocation_ShouldReturnRoomsWithinRadius()
    {
        if (_repository is null) Assert.Ignore("Cosmos DB repository is not initialized.");

        var query = new SearchRoomsQuery(
            latitude: 21.5852,
            longitude: 105.8073,
            radiusInKm: 2.0,
            keyword: _batchId);

        var (items, totalCount) = await _repository!.SearchAsync(query);

        Assert.That(totalCount, Is.GreaterThanOrEqualTo(1));
        var itemIds = items.Select(r => r.Id).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(itemIds, Does.Contain($"room-near-{_batchId}"));
            Assert.That(itemIds, Does.Not.Contain($"room-far-{_batchId}"));
        });
    }

    [Test]
    public async Task SearchAsync_ShouldHandlePaginationCorrectly()
    {
        if (_repository is null) Assert.Ignore("Cosmos DB repository is not initialized.");

        var page1Query = new SearchRoomsQuery(keyword: _batchId, pageNumber: 1, pageSize: 1);
        var (page1Items, totalCount) = await _repository!.SearchAsync(page1Query);

        Assert.That(totalCount, Is.GreaterThanOrEqualTo(2));
        Assert.That(page1Items.Count, Is.EqualTo(1));

        var page2Query = new SearchRoomsQuery(keyword: _batchId, pageNumber: 2, pageSize: 1);
        var (page2Items, totalCount2) = await _repository!.SearchAsync(page2Query);

        Assert.That(totalCount2, Is.EqualTo(totalCount));
        Assert.That(page2Items.Count, Is.EqualTo(1));
        Assert.That(page1Items[0].Id, Is.Not.EqualTo(page2Items[0].Id));
    }
}