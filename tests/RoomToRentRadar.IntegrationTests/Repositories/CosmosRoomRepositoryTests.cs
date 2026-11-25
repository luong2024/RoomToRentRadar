using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Caching.Memory;
using RoomToRentRadar.Adapters.Outbound.Caching;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.UseCases.RoomListings;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.ValueObjects;

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
        _client = CreateCosmosClient();
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
            containerProperties.IndexingPolicy.CompositeIndexes.Add(new System.Collections.ObjectModel.Collection<CompositePath>
            {
                new() { Path = "/price", Order = CompositePathSortOrder.Ascending },
                new() { Path = "/id", Order = CompositePathSortOrder.Ascending }
            });
            containerProperties.IndexingPolicy.CompositeIndexes.Add(new System.Collections.ObjectModel.Collection<CompositePath>
            {
                new() { Path = "/price", Order = CompositePathSortOrder.Descending },
                new() { Path = "/id", Order = CompositePathSortOrder.Descending }
            });
            containerProperties.IndexingPolicy.CompositeIndexes.Add(new System.Collections.ObjectModel.Collection<CompositePath>
            {
                new() { Path = "/createdAt", Order = CompositePathSortOrder.Descending },
                new() { Path = "/id", Order = CompositePathSortOrder.Descending }
            });

            var containerResponse = await _database.CreateContainerIfNotExistsAsync(containerProperties);
            _container = containerResponse.Container;

            // Ensure composite indexes exist if container already existed
            await EnsureContainerIndexesAsync(_container, containerResponse.Resource);

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

        var targetPrice = 15000000m;
        var targetArea = 150.0;

        var targetRoom = new Room
        {
            Id = targetRoomId,
            Title = "Phòng mục tiêu (đã thuê)",
            Price = targetPrice,
            Area = targetArea,
            Status = RoomStatus.Rented
        };

        var matchingRoom1 = new Room { Id = $"match1-{testBatchId}", Title = "Phòng tương tự chuẩn 1", Price = 15500000m, Area = 155.0, Status = RoomStatus.Available, IsVerified = true };
        var matchingRoom2 = new Room { Id = $"match2-{testBatchId}", Title = "Phòng tương tự chuẩn 2", Price = 14000000m, Area = 140.0, Status = RoomStatus.Available, IsVerified = true };
        var areaTooSmallRoom = new Room { Id = $"small-area-{testBatchId}", Title = "Phòng diện tích quá nhỏ", Price = targetPrice, Area = 90.0, Status = RoomStatus.Available, IsVerified = true };
        var areaTooLargeRoom = new Room { Id = $"large-area-{testBatchId}", Title = "Phòng diện tích quá lớn", Price = targetPrice, Area = 210.0, Status = RoomStatus.Available, IsVerified = true };
        var priceTooCheapRoom = new Room { Id = $"cheap-price-{testBatchId}", Title = "Phòng giá quá rẻ", Price = 9000000m, Area = targetArea, Status = RoomStatus.Available, IsVerified = true };
        var priceTooExpensiveRoom = new Room { Id = $"expensive-price-{testBatchId}", Title = "Phòng giá quá đắt", Price = 22000000m, Area = targetArea, Status = RoomStatus.Available, IsVerified = true };
        var nonAvailableRoom = new Room { Id = $"rented-{testBatchId}", Title = "Phòng đã cho thuê", Price = targetPrice, Area = targetArea, Status = RoomStatus.Rented, IsVerified = true };
        var unverifiedRoom = new Room { Id = $"unverified-{testBatchId}", Title = "Phòng chưa xác minh", Price = targetPrice, Area = targetArea, Status = RoomStatus.Available, IsVerified = false };

        await _repository!.CreateAsync(targetRoom);
        await _repository.CreateAsync(matchingRoom1);
        await _repository.CreateAsync(matchingRoom2);
        await _repository.CreateAsync(areaTooSmallRoom);
        await _repository.CreateAsync(areaTooLargeRoom);
        await _repository.CreateAsync(priceTooCheapRoom);
        await _repository.CreateAsync(priceTooExpensiveRoom);
        await _repository.CreateAsync(nonAvailableRoom);
        await _repository.CreateAsync(unverifiedRoom);

        var similarRooms = await _repository.GetSimilarRoomsAsync(targetRoomId, price: targetPrice, area: targetArea);

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
            Assert.That(similarIds, Does.Not.Contain(unverifiedRoom.Id), "Phòng chưa xác minh (IsVerified = false) phải bị loại khỏi phòng tương tự.");
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

    [Test]
    public async Task ListingWrites_RoundTripFullPrecisionAndGeoJsonWithFreshCosmosClients()
    {
        var owner = Guid.NewGuid();
        var repository = _repository!;
        var images = new FakeListingImageStorageService();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var cacheInvalidator = new RoomListingCacheInvalidator(new MemoryCacheService(cache));
        var update = new UpdateListingUseCase(repository, images, new InMemoryRoomListingRepository(), cacheInvalidator);
        var original = new GeoLocation(21.5944123456789, 105.8482123456789);
        var replacement = new GeoLocation(-33.857123456789, 151.213123456789);

        var created = await new CreateListingUseCase(repository, images).ExecuteAsync(
            owner, ListingRequest(original, photoCount: 3));
        var createdDocument = await ReadFreshRoomDocumentAsync(created.ListingId);
        AssertStoredLocation(createdDocument, original);

        var updated = await update.ExecuteAsync(owner, created.ListingId, ListingRequest(replacement));
        var updatedDocument = await ReadFreshRoomDocumentAsync(created.ListingId);
        AssertStoredLocation(updatedDocument, replacement);
        Assert.That(updatedDocument.PendingVisibilitySyncId, Is.Not.Null);
        Assert.That(updated.Location, Is.EqualTo(replacement));

        await update.ExecuteAsync(owner, created.ListingId, ListingRequest());
        var beforeInvalidUpdate = await ReadFreshRoomDocumentAsync(created.ListingId);
        var invalidUpdate = ListingRequest(photoCount: 1) with { Title = "Do not save", Latitude = 22 };
        Assert.ThrowsAsync<RequestValidationException>(() => update.ExecuteAsync(owner, created.ListingId, invalidUpdate));

        var afterInvalidUpdate = await ReadFreshRoomDocumentAsync(created.ListingId);
        Assert.Multiple(() =>
        {
            Assert.That(afterInvalidUpdate.ETag, Is.EqualTo(beforeInvalidUpdate.ETag));
            Assert.That(afterInvalidUpdate.Title, Is.EqualTo(beforeInvalidUpdate.Title));
            Assert.That(afterInvalidUpdate.ImageUrls, Is.EqualTo(beforeInvalidUpdate.ImageUrls));
            Assert.That(images.StoredUrls, Has.Count.EqualTo(3));
        });

        using var reader = CreateCosmosClient();
        var freshRepository = new CosmosRoomRepository(reader.GetContainer(DatabaseName, ContainerName));
        var ownerRead = await new GetListingCoordinatesUseCase(freshRepository).ExecuteAsync(owner, created.ListingId);
        Assert.That(ownerRead.Location, Is.EqualTo(replacement));

        using var rawResponse = await reader.GetContainer(DatabaseName, ContainerName).ReadItemStreamAsync(
            created.ListingId, new PartitionKey(created.ListingId));
        Assert.That(rawResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var rawDocument = await JsonDocument.ParseAsync(rawResponse.Content);
        var document = rawDocument.RootElement;
        var point = document.GetProperty("location");
        Assert.Multiple(() =>
        {
            Assert.That(document.GetProperty("latitude").GetDouble(), Is.EqualTo(replacement.Latitude));
            Assert.That(document.GetProperty("longitude").GetDouble(), Is.EqualTo(replacement.Longitude));
            Assert.That(point.GetProperty("type").GetString(), Is.EqualTo("Point"));
            Assert.That(point.GetProperty("coordinates").EnumerateArray().Select(value => value.GetDouble()),
                Is.EqualTo(new[] { replacement.Longitude, replacement.Latitude }));
        });
    }

    [TestCase("point-only", null, null, true, 22.25, 106.75)]
    [TestCase("half-pair", 21.25, null, false, null, null)]
    [TestCase("different-point", 21.25, 105.75, true, 21.25, 105.75)]
    [TestCase("no-location", null, null, false, null, null)]
    public async Task LegacyCoordinates_OmittedUpdatePreservesScalarsAndPointAndExplicitUpdateReplacesBoth(
        string scenario, double? latitude, double? longitude, bool hasPoint, double? expectedLatitude, double? expectedLongitude)
    {
        var id = $"legacy-coordinates-{scenario}-{Guid.NewGuid():N}";
        var owner = Guid.NewGuid();
        var original = new RoomDocument
        {
            Id = id,
            LandlordId = owner,
            Latitude = latitude,
            Longitude = longitude,
            Location = hasPoint ? new CosmosPoint { Coordinates = [106.75, 22.25] } : null
        };
        await _container!.CreateItemAsync(original, new PartitionKey(id));
        var repository = _repository!;
        var room = (await repository.GetByIdAsync(id))!;
        Assert.That(room.GetLocation(), Is.EqualTo(GeoLocation.FromOptional(expectedLatitude, expectedLongitude)));

        room.Title = "Updated title";
        await repository.UpdateAsync(room);
        var preserved = await ReadFreshRoomDocumentAsync(id);
        Assert.Multiple(() =>
        {
            Assert.That(preserved.Title, Is.EqualTo("Updated title"));
            Assert.That(preserved.Latitude, Is.EqualTo(latitude));
            Assert.That(preserved.Longitude, Is.EqualTo(longitude));
            Assert.That(preserved.Location?.Coordinates, Is.EqualTo(original.Location?.Coordinates));
        });

        var loaded = (await repository.GetByIdAsync(id))!;
        var explicitLocation = new GeoLocation(0, 0);
        await repository.UpdateAsync(loaded, explicitLocation);
        AssertStoredLocation(await ReadFreshRoomDocumentAsync(id), explicitLocation);
    }

    [Test]
    public async Task LegacyCoordinates_PointWithoutCoordinatesDoesNotBecomeZeroZero()
    {
        var id = $"legacy-missing-point-coordinates-{Guid.NewGuid():N}";
        using var payload = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new { id, location = new { type = "Point" } }));
        using var response = await _container!.CreateItemStreamAsync(payload, new PartitionKey(id));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var room = (await _repository!.GetByIdAsync(id))!;
        Assert.Multiple(() =>
        {
            Assert.That(room.GetLocation(), Is.Null);
            Assert.That(room.Latitude, Is.Null);
            Assert.That(room.Longitude, Is.Null);
        });
    }

    [Test]
    public async Task ListingCoordinateUpdate_StaleETagDoesNotOverwritePairOrVisibilityIntent()
    {
        var id = $"concurrent-coordinates-{Guid.NewGuid():N}";
        var repository = _repository!;
        await repository.CreateAsync(new Room { Id = id, LandlordId = Guid.NewGuid(), Status = RoomStatus.Pending });
        var first = (await repository.GetByIdAsync(id))!;
        var stale = (await repository.GetByIdAsync(id))!;

        var savedLocation = new GeoLocation(21.5, 105.8);
        first.Title = "Winning update";
        await repository.UpdateAsync(first, savedLocation);
        var saved = await ReadFreshRoomDocumentAsync(id);

        stale.Title = "Stale update";
        Assert.ThrowsAsync<ConcurrencyConflictException>(() => repository.UpdateAsync(stale, new GeoLocation(22, 106)));
        var afterConflict = await ReadFreshRoomDocumentAsync(id);
        AssertStoredLocation(afterConflict, savedLocation);
        Assert.Multiple(() =>
        {
            Assert.That(afterConflict.Title, Is.EqualTo("Winning update"));
            Assert.That(afterConflict.PendingVisibilitySyncId, Is.Not.Null.And.EqualTo(saved.PendingVisibilitySyncId));
        });
    }

    private static ListingWriteRequest ListingRequest(GeoLocation? location = null, int photoCount = 0) => new(
        "Coordinate listing", "10 Test Street", 2_000_000m, 0, 20, "BoardingRoom", null,
        Enumerable.Range(0, photoCount).Select(i => new ListingImageUpload(
            () => new MemoryStream([1, 2, 3]), $"location-photo-{i}.png", "image/png", 3)).ToArray(),
        Latitude: location?.Latitude, Longitude: location?.Longitude);

    private static async Task<RoomDocument> ReadFreshRoomDocumentAsync(string id)
    {
        using var reader = CreateCosmosClient();
        var response = await reader.GetContainer(DatabaseName, ContainerName).ReadItemAsync<RoomDocument>(id, new PartitionKey(id));
        return response.Resource;
    }

    private static void AssertStoredLocation(RoomDocument document, GeoLocation location)
    {
        Assert.Multiple(() =>
        {
            Assert.That(document.Latitude, Is.EqualTo(location.Latitude));
            Assert.That(document.Longitude, Is.EqualTo(location.Longitude));
            Assert.That(document.Location?.Type, Is.EqualTo("Point"));
            Assert.That(document.Location?.Coordinates, Is.EqualTo(new[] { location.Longitude, location.Latitude }));
        });
    }

    private static CosmosClient CreateCosmosClient()
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
        return new CosmosClient(Endpoint, AuthKey, clientOptions);
    }

    [Test]
    public async Task SearchAsync_AfterCreateAsyncWithCoordinates_ShouldFindRoomWithinGpsRadius()
    {
        if (_repository is null) Assert.Ignore("Cosmos DB repository is not initialized.");

        var testId = Guid.NewGuid().ToString("N")[..8];
        var uniqueKeyword = $"created-gps-{testId}";

        var room = new Room
        {
            Id = $"room-created-{testId}",
            Title = $"Phòng tạo mới qua CreateAsync {uniqueKeyword}",
            Description = "Kiểm tra GPS radius search sau khi CreateAsync",
            Price = 2000000m,
            Area = 25.0,
            RoomType = "Khép kín",
            Address = "Đường Hoàng Văn Thụ, Thái Nguyên",
            Latitude = 21.5860,
            Longitude = 105.8080,
            Status = RoomStatus.Available,
            IsVerified = true
        };

        // Act - Lưu phòng qua CreateAsync (phải serialize qua RoomDocument và lưu GeoJSON Location)
        var createdRoom = await _repository!.CreateAsync(room);
        Assert.That(createdRoom, Is.Not.Null);
        Assert.That(createdRoom.Id, Is.EqualTo(room.Id));

        // Search với toạ độ gần đó (tâm 21.5852, 105.8073, bán kính 2km)
        var query = new SearchRoomsQuery(
            latitude: 21.5852,
            longitude: 105.8073,
            radiusInKm: 2.0,
            keyword: uniqueKeyword);

        var (items, totalCount) = await _repository.SearchAsync(query);

        // Assert - Room được tạo bởi CreateAsync phải được tìm thấy qua GPS radius search
        Assert.Multiple(() =>
        {
            Assert.That(totalCount, Is.EqualTo(1));
            Assert.That(items.Count, Is.EqualTo(1));
            Assert.That(items[0].Id, Is.EqualTo(room.Id));
            Assert.That(items[0].Latitude, Is.EqualTo(room.Latitude));
            Assert.That(items[0].Longitude, Is.EqualTo(room.Longitude));
        });
    }

    [Test]
    public async Task SearchAsync_WithPriceAscSort_ReturnsCrossPageItemsInCorrectOrder()
    {
        if (_repository is null) Assert.Ignore("Cosmos DB repository is not initialized.");

        var testId = Guid.NewGuid().ToString("N")[..8];
        var uniqueKeyword = $"sort-price-asc-{testId}";

        var room1 = new Room { Id = $"p1-{testId}", Title = $"Phòng {uniqueKeyword}", Price = 1_000_000m, Area = 20, Status = RoomStatus.Available, IsVerified = true };
        var room2 = new Room { Id = $"p2-{testId}", Title = $"Phòng {uniqueKeyword}", Price = 2_000_000m, Area = 20, Status = RoomStatus.Available, IsVerified = true };
        var room3 = new Room { Id = $"p3-{testId}", Title = $"Phòng {uniqueKeyword}", Price = 3_000_000m, Area = 20, Status = RoomStatus.Available, IsVerified = true };
        var room4 = new Room { Id = $"p4-{testId}", Title = $"Phòng {uniqueKeyword}", Price = 4_000_000m, Area = 20, Status = RoomStatus.Available, IsVerified = true };

        await _repository.CreateAsync(room3);
        await _repository.CreateAsync(room1);
        await _repository.CreateAsync(room4);
        await _repository.CreateAsync(room2);

        var page1Query = new SearchRoomsQuery(keyword: uniqueKeyword, sortBy: "PriceAsc", pageNumber: 1, pageSize: 2);
        var (page1Items, totalCount1) = await _repository.SearchAsync(page1Query);

        var page2Query = new SearchRoomsQuery(keyword: uniqueKeyword, sortBy: "PriceAsc", pageNumber: 2, pageSize: 2);
        var (page2Items, totalCount2) = await _repository.SearchAsync(page2Query);

        Assert.Multiple(() =>
        {
            Assert.That(totalCount1, Is.EqualTo(4));
            Assert.That(totalCount2, Is.EqualTo(4));
            Assert.That(page1Items.Select(r => r.Price), Is.EqualTo(new[] { 1_000_000m, 2_000_000m }));
            Assert.That(page2Items.Select(r => r.Price), Is.EqualTo(new[] { 3_000_000m, 4_000_000m }));
        });
    }

    [Test]
    public async Task SearchAsync_WithPriceDescSort_ReturnsCrossPageItemsInCorrectOrder()
    {
        if (_repository is null) Assert.Ignore("Cosmos DB repository is not initialized.");

        var testId = Guid.NewGuid().ToString("N")[..8];
        var uniqueKeyword = $"sort-price-desc-{testId}";

        var room1 = new Room { Id = $"pd1-{testId}", Title = $"Phòng {uniqueKeyword}", Price = 1_500_000m, Area = 20, Status = RoomStatus.Available, IsVerified = true };
        var room2 = new Room { Id = $"pd2-{testId}", Title = $"Phòng {uniqueKeyword}", Price = 2_500_000m, Area = 20, Status = RoomStatus.Available, IsVerified = true };
        var room3 = new Room { Id = $"pd3-{testId}", Title = $"Phòng {uniqueKeyword}", Price = 3_500_000m, Area = 20, Status = RoomStatus.Available, IsVerified = true };
        var room4 = new Room { Id = $"pd4-{testId}", Title = $"Phòng {uniqueKeyword}", Price = 4_500_000m, Area = 20, Status = RoomStatus.Available, IsVerified = true };

        await _repository.CreateAsync(room2);
        await _repository.CreateAsync(room4);
        await _repository.CreateAsync(room1);
        await _repository.CreateAsync(room3);

        var page1Query = new SearchRoomsQuery(keyword: uniqueKeyword, sortBy: "PriceDesc", pageNumber: 1, pageSize: 2);
        var (page1Items, totalCount1) = await _repository.SearchAsync(page1Query);

        var page2Query = new SearchRoomsQuery(keyword: uniqueKeyword, sortBy: "PriceDesc", pageNumber: 2, pageSize: 2);
        var (page2Items, totalCount2) = await _repository.SearchAsync(page2Query);

        Assert.Multiple(() =>
        {
            Assert.That(totalCount1, Is.EqualTo(4));
            Assert.That(totalCount2, Is.EqualTo(4));
            Assert.That(page1Items.Select(r => r.Price), Is.EqualTo(new[] { 4_500_000m, 3_500_000m }));
            Assert.That(page2Items.Select(r => r.Price), Is.EqualTo(new[] { 2_500_000m, 1_500_000m }));
        });
    }

    [Test]
    public async Task SearchAsync_WithDistanceAscSort_OrdersBySpatialProximity()
    {
        if (_repository is null) Assert.Ignore("Cosmos DB repository is not initialized.");

        var testId = Guid.NewGuid().ToString("N")[..8];
        var uniqueKeyword = $"sort-dist-{testId}";

        // Center: ICTU (21.5852, 105.8073)
        // Near (~0.3km)
        var roomNear = new Room { Id = $"dist-near-{testId}", Title = $"Gần {uniqueKeyword}", Price = 2_000_000m, Area = 20, Latitude = 21.587, Longitude = 105.808, Status = RoomStatus.Available, IsVerified = true };
        // Mid (~2km)
        var roomMid = new Room { Id = $"dist-mid-{testId}", Title = $"Vừa {uniqueKeyword}", Price = 2_000_000m, Area = 20, Latitude = 21.600, Longitude = 105.815, Status = RoomStatus.Available, IsVerified = true };
        // Far (~15km)
        var roomFar = new Room { Id = $"dist-far-{testId}", Title = $"Xa {uniqueKeyword}", Price = 2_000_000m, Area = 20, Latitude = 21.700, Longitude = 105.900, Status = RoomStatus.Available, IsVerified = true };

        await _repository.CreateAsync(roomFar);
        await _repository.CreateAsync(roomNear);
        await _repository.CreateAsync(roomMid);

        var query = new SearchRoomsQuery(keyword: uniqueKeyword, latitude: 21.5852, longitude: 105.8073, sortBy: "DistanceAsc", pageNumber: 1, pageSize: 10);
        var (items, totalCount) = await _repository.SearchAsync(query);

        Assert.Multiple(() =>
        {
            Assert.That(totalCount, Is.EqualTo(3));
            Assert.That(items.Count, Is.EqualTo(3));
            Assert.That(items[0].Id, Is.EqualTo(roomNear.Id));
            Assert.That(items[1].Id, Is.EqualTo(roomMid.Id));
            Assert.That(items[2].Id, Is.EqualTo(roomFar.Id));
        });
    }

    [Test]
    public async Task SearchAsync_WithNewestSort_OrdersByCreatedAtDescending_EvenWhenUpdatedLater()
    {
        if (_repository is null) Assert.Ignore("Cosmos DB repository is not initialized.");

        var testId = Guid.NewGuid().ToString("N")[..8];
        var uniqueKeyword = $"sort-newest-{testId}";

        var baseDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        // Room A: Created Nov 1, Updated Nov 25 (Updated very late)
        var roomA = new Room
        {
            Id = $"newest-older-{testId}",
            Title = $"Phòng cũ vừa sửa {uniqueKeyword}",
            Price = 2_000_000m,
            Area = 20,
            CreatedAt = baseDate.AddDays(1),
            LastUpdatedDate = baseDate.AddDays(25),
            Status = RoomStatus.Available,
            IsVerified = true
        };

        // Room B: Created Nov 15, Updated Nov 15 (Created more recently)
        var roomB = new Room
        {
            Id = $"newest-newer-{testId}",
            Title = $"Phòng mới tạo {uniqueKeyword}",
            Price = 2_000_000m,
            Area = 20,
            CreatedAt = baseDate.AddDays(15),
            LastUpdatedDate = baseDate.AddDays(15),
            Status = RoomStatus.Available,
            IsVerified = true
        };

        await _repository.CreateAsync(roomA);
        await _repository.CreateAsync(roomB);

        var query = new SearchRoomsQuery(keyword: uniqueKeyword, sortBy: "Newest", pageNumber: 1, pageSize: 10);
        var (items, totalCount) = await _repository.SearchAsync(query);

        Assert.Multiple(() =>
        {
            Assert.That(totalCount, Is.EqualTo(2));
            Assert.That(items.Count, Is.EqualTo(2));
            Assert.That(items[0].Id, Is.EqualTo(roomB.Id), "Room B (created Nov 15) must come before Room A (created Nov 1).");
            Assert.That(items[1].Id, Is.EqualTo(roomA.Id));
        });
    }

    [Test]
    public async Task SearchAsync_WithAmenitiesCombinedWithAreaAndDistance_ReturnsOnlyMatchingRooms()
    {
        var uniqueKeyword = $"amenity-test-{Guid.NewGuid():N}";
        var matchingRoom = new Room
        {
            Id = $"room-amenity-match-{Guid.NewGuid():N}",
            Title = $"{uniqueKeyword} Phòng phù hợp tiện ích",
            Description = "Mô tả",
            Address = "Địa chỉ gần trường",
            Price = 2_000_000m,
            Area = 25.0,
            RoomType = "Khép kín",
            Latitude = 21.5860,
            Longitude = 105.8080,
            Status = RoomStatus.Available,
            IsVerified = true,
            Amenities = ["Wifi", "Aircon"]
        };

        var roomMissingAmenity = new Room
        {
            Id = $"room-amenity-missing-{Guid.NewGuid():N}",
            Title = $"{uniqueKeyword} Phòng thiếu tiện ích",
            Description = "Mô tả",
            Address = "Địa chỉ gần trường",
            Price = 2_000_000m,
            Area = 25.0,
            RoomType = "Khép kín",
            Latitude = 21.5860,
            Longitude = 105.8080,
            Status = RoomStatus.Available,
            IsVerified = true,
            Amenities = ["Wifi"]
        };

        var roomOutOfArea = new Room
        {
            Id = $"room-amenity-area-{Guid.NewGuid():N}",
            Title = $"{uniqueKeyword} Phòng diện tích nhỏ",
            Description = "Mô tả",
            Address = "Địa chỉ gần trường",
            Price = 2_000_000m,
            Area = 15.0,
            RoomType = "Khép kín",
            Latitude = 21.5860,
            Longitude = 105.8080,
            Status = RoomStatus.Available,
            IsVerified = true,
            Amenities = ["Wifi", "Aircon"]
        };

        await _repository!.CreateAsync(matchingRoom);
        await _repository.CreateAsync(roomMissingAmenity);
        await _repository.CreateAsync(roomOutOfArea);

        var query = new SearchRoomsQuery(
            keyword: uniqueKeyword,
            minArea: 20.0,
            maxArea: 30.0,
            latitude: 21.5852,
            longitude: 105.8073,
            radiusInKm: 5.0,
            amenities: ["Wifi", "Aircon"],
            pageNumber: 1,
            pageSize: 10);

        var (items, totalCount) = await _repository.SearchAsync(query);

        Assert.Multiple(() =>
        {
            Assert.That(totalCount, Is.EqualTo(1));
            Assert.That(items.Count, Is.EqualTo(1));
            Assert.That(items[0].Id, Is.EqualTo(matchingRoom.Id));
            Assert.That(items[0].Amenities, Does.Contain("Wifi"));
            Assert.That(items[0].Amenities, Does.Contain("Aircon"));
        });
    }

    private static async Task EnsureContainerIndexesAsync(Container container, ContainerProperties properties)
    {
        var indexingPolicy = properties.IndexingPolicy ?? new IndexingPolicy();
        properties.IndexingPolicy = indexingPolicy;
        var modified = false;

        var locationIndex = indexingPolicy.SpatialIndexes.FirstOrDefault(index =>
            index.Path.Equals("/location/*", StringComparison.Ordinal));

        if (locationIndex is null)
        {
            indexingPolicy.SpatialIndexes.Add(new SpatialPath
            {
                Path = "/location/*",
                SpatialTypes = { SpatialType.Point }
            });
            modified = true;
        }

        if (!HasCompositeIndex(indexingPolicy, "/price", CompositePathSortOrder.Ascending, "/id", CompositePathSortOrder.Ascending))
        {
            indexingPolicy.CompositeIndexes.Add(new System.Collections.ObjectModel.Collection<CompositePath>
            {
                new() { Path = "/price", Order = CompositePathSortOrder.Ascending },
                new() { Path = "/id", Order = CompositePathSortOrder.Ascending }
            });
            modified = true;
        }

        if (!HasCompositeIndex(indexingPolicy, "/price", CompositePathSortOrder.Descending, "/id", CompositePathSortOrder.Descending))
        {
            indexingPolicy.CompositeIndexes.Add(new System.Collections.ObjectModel.Collection<CompositePath>
            {
                new() { Path = "/price", Order = CompositePathSortOrder.Descending },
                new() { Path = "/id", Order = CompositePathSortOrder.Descending }
            });
            modified = true;
        }

        if (!HasCompositeIndex(indexingPolicy, "/createdAt", CompositePathSortOrder.Descending, "/id", CompositePathSortOrder.Descending))
        {
            indexingPolicy.CompositeIndexes.Add(new System.Collections.ObjectModel.Collection<CompositePath>
            {
                new() { Path = "/createdAt", Order = CompositePathSortOrder.Descending },
                new() { Path = "/id", Order = CompositePathSortOrder.Descending }
            });
            modified = true;
        }

        if (modified)
        {
            await container.ReplaceContainerAsync(properties);
        }
    }

    private static bool HasCompositeIndex(IndexingPolicy policy, string path1, CompositePathSortOrder order1, string path2, CompositePathSortOrder order2)
    {
        return policy.CompositeIndexes.Any(ci =>
            ci.Count >= 2 &&
            string.Equals(ci[0].Path, path1, StringComparison.Ordinal) &&
            ci[0].Order == order1 &&
            string.Equals(ci[1].Path, path2, StringComparison.Ordinal) &&
            ci[1].Order == order2);
    }
}