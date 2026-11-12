using System.Net;
using Microsoft.Azure.Cosmos;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.IntegrationTests.Repositories;

[TestFixture]
public class CosmosRoomRepositoryTests
{
    private const string Endpoint = "https://localhost:8081";
    private const string AuthKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    private const string DatabaseName = "RoomToRentRadarDb_Integration";
    private const string ContainerName = "Rooms_Test";

    private CosmosClient _client = null!;
    private Database _database = null!;
    private Container _container = null!;
    private CosmosRoomRepository _repository = null!;

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
            _database = dbResponse.Database;

            var containerResponse = await _database.CreateContainerIfNotExistsAsync(ContainerName, "/id");
            _container = containerResponse.Container;

            _repository = new CosmosRoomRepository(_container);
        }
        catch (Exception ex) when (ex is HttpRequestException ||
                                  ex is System.Net.Sockets.SocketException ||
                                  ex.InnerException is HttpRequestException ||
                                  ex.InnerException is System.Net.Sockets.SocketException ||
                                  ex.Message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore($"Cosmos DB Emulator is not reachable at {Endpoint}. Skipping live integration tests on environments without running emulator. Details: {ex.Message}");
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
                // Silently ignore cleanup errors if database was already deleted
            }
        }

        _client?.Dispose();
    }

    [Test]
    public async Task AddRoom_DirectToCosmosEmulator_ShouldSucceed()
    {
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
        var created = await _repository.CreateAsync(room);

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
        await _repository.CreateAsync(room);

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
        // Target room parameters:
        // Price = 3,000,000 => range [2,100,000 ; 3,900,000] (0.7 to 1.3)
        // Area  = 30.0      => range [21.0 ; 39.0]           (0.7 to 1.3)
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

        // 1. Thỏa mãn cả Giá và Diện tích (Status = Available) -> Mong muốn trả về
        var matchingRoom1 = new Room
        {
            Id = $"match1-{testBatchId}",
            Title = "Phòng tương tự chuẩn 1",
            Price = 3100000m,
            Area = 31.0,
            Status = RoomStatus.Available
        };

        var matchingRoom2 = new Room
        {
            Id = $"match2-{testBatchId}",
            Title = "Phòng tương tự chuẩn 2",
            Price = 2500000m,
            Area = 26.0,
            Status = RoomStatus.Available
        };

        // 2. Thỏa mãn giá nhưng Diện tích quá NHỎ (< 21.0) -> Bị loại
        var areaTooSmallRoom = new Room
        {
            Id = $"small-area-{testBatchId}",
            Title = "Phòng diện tích quá nhỏ",
            Price = 3000000m,
            Area = 18.0,
            Status = RoomStatus.Available
        };

        // 3. Thỏa mãn giá nhưng Diện tích quá LỚN (> 39.0) -> Bị loại
        var areaTooLargeRoom = new Room
        {
            Id = $"large-area-{testBatchId}",
            Title = "Phòng diện tích quá lớn",
            Price = 3000000m,
            Area = 45.0,
            Status = RoomStatus.Available
        };

        // 4. Thỏa mãn diện tích nhưng Giá quá RẺ (< 2,100,000) -> Bị loại
        var priceTooCheapRoom = new Room
        {
            Id = $"cheap-price-{testBatchId}",
            Title = "Phòng giá quá rẻ",
            Price = 1800000m,
            Area = 30.0,
            Status = RoomStatus.Available
        };

        // 5. Thỏa mãn diện tích nhưng Giá quá ĐẮT (> 3,900,000) -> Bị loại
        var priceTooExpensiveRoom = new Room
        {
            Id = $"expensive-price-{testBatchId}",
            Title = "Phòng giá quá đắt",
            Price = 4500000m,
            Area = 30.0,
            Status = RoomStatus.Available
        };

        // 6. Thỏa mãn giá và diện tích nhưng không Available -> Bị loại
        var nonAvailableRoom = new Room
        {
            Id = $"rented-{testBatchId}",
            Title = "Phòng đã cho thuê",
            Price = 3000000m,
            Area = 30.0,
            Status = RoomStatus.Rented
        };

        // Seed all test rooms to Cosmos DB container
        await _repository.CreateAsync(targetRoom);
        await _repository.CreateAsync(matchingRoom1);
        await _repository.CreateAsync(matchingRoom2);
        await _repository.CreateAsync(areaTooSmallRoom);
        await _repository.CreateAsync(areaTooLargeRoom);
        await _repository.CreateAsync(priceTooCheapRoom);
        await _repository.CreateAsync(priceTooExpensiveRoom);
        await _repository.CreateAsync(nonAvailableRoom);

        // Act - Call GetSimilarRoomsAsync for target room
        var similarRooms = await _repository.GetSimilarRoomsAsync(
            targetRoomId,
            price: 3000000m,
            area: 30.0);

        // Assert
        Assert.That(similarRooms, Is.Not.Null);
        var similarIds = similarRooms.Select(r => r.Id).ToList();

        Assert.Multiple(() =>
        {
            // Matching rooms must be returned
            Assert.That(similarIds, Does.Contain(matchingRoom1.Id), "Phòng thỏa mãn giá và diện tích (match1) phải có trong kết quả.");
            Assert.That(similarIds, Does.Contain(matchingRoom2.Id), "Phòng thỏa mãn giá và diện tích (match2) phải có trong kết quả.");

            // Filtered out rooms must NOT be returned
            Assert.That(similarIds, Does.Not.Contain(targetRoom.Id), "Chính phòng target không được có trong kết quả tương tự.");
            Assert.That(similarIds, Does.Not.Contain(areaTooSmallRoom.Id), "Phòng diện tích quá nhỏ không được có trong kết quả.");
            Assert.That(similarIds, Does.Not.Contain(areaTooLargeRoom.Id), "Phòng diện tích quá lớn không được có trong kết quả.");
            Assert.That(similarIds, Does.Not.Contain(priceTooCheapRoom.Id), "Phòng giá quá rẻ không được có trong kết quả.");
            Assert.That(similarIds, Does.Not.Contain(priceTooExpensiveRoom.Id), "Phòng giá quá đắt không được có trong kết quả.");
            Assert.That(similarIds, Does.Not.Contain(nonAvailableRoom.Id), "Phòng không Available không được có trong kết quả.");
        });
    }
}
