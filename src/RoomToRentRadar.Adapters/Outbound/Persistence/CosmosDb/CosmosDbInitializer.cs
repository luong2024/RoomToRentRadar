namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Configurations;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

public sealed class CosmosDbInitializer
{
    private readonly CosmosClient _cosmosClient;
    private readonly CosmosDbOptions _settings;
    private readonly ILogger<CosmosDbInitializer> _logger;

    public CosmosDbInitializer(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        ILogger<CosmosDbInitializer> logger)
    {
        _cosmosClient = cosmosClient;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initializing Cosmos DB database '{DatabaseName}'...", _settings.DatabaseName);

            var dbResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_settings.DatabaseName, cancellationToken: cancellationToken);
            var database = dbResponse.Database;

            _logger.LogInformation("Creating container '{CampusesContainerName}' (/id)...", _settings.CampusesContainerName);
            var campusesContainerResponse = await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(_settings.CampusesContainerName, "/id"),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Creating container '{RoomsContainerName}' (/campusId)...", _settings.ContainerName);
            var roomsContainerResponse = await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(_settings.ContainerName, "/campusId"),
                cancellationToken: cancellationToken);

            var partitionKeyPath = roomsContainerResponse.Resource.PartitionKeyPath;
            if (!string.Equals(partitionKeyPath, "/campusId", StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Cosmos container '{ContainerName}' currently has partition key '{PartitionKeyPath}', but '/campusId' is required. " +
                    "Cosmos DB cannot modify the partition key in-place. Initiating automated data migration...",
                    _settings.ContainerName,
                    partitionKeyPath);

                roomsContainerResponse = await MigrateContainerToCampusIdPartitionAsync(
                    database,
                    roomsContainerResponse.Container,
                    _settings.ContainerName,
                    cancellationToken);
            }

            await EnsureSpatialAndCompositeIndexesAsync(roomsContainerResponse.Container, roomsContainerResponse.Resource, cancellationToken);

            await SeedInitialDataAsync(campusesContainerResponse.Container, roomsContainerResponse.Container, cancellationToken);

            _logger.LogInformation("Cosmos DB initialization completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Cosmos DB.");
            throw;
        }
    }

    private async Task<ContainerResponse> MigrateContainerToCampusIdPartitionAsync(
        Database database,
        Container oldContainer,
        string containerName,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("Beginning automated migration of container '{ContainerName}' to partition key '/campusId'...", containerName);

        var existingDocs = new List<Newtonsoft.Json.Linq.JObject>();
        try
        {
            using var feedIterator = oldContainer.GetItemQueryIterator<Newtonsoft.Json.Linq.JObject>(new QueryDefinition("SELECT * FROM c"));
            while (feedIterator.HasMoreResults)
            {
                var feedResponse = await feedIterator.ReadNextAsync(cancellationToken);
                existingDocs.AddRange(feedResponse);
            }
            _logger.LogInformation("Read {Count} existing documents from old container '{ContainerName}' for migration.", existingDocs.Count, containerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read documents from old container '{ContainerName}' before migration.", containerName);
            throw;
        }

        _logger.LogInformation("Deleting container '{ContainerName}' with legacy partition key...", containerName);
        await oldContainer.DeleteContainerAsync(cancellationToken: cancellationToken);

        _logger.LogInformation("Recreating container '{ContainerName}' with partition key '/campusId'...", containerName);
        var containerProperties = new ContainerProperties(containerName, "/campusId");
        var newContainerResponse = await database.CreateContainerAsync(containerProperties, cancellationToken: cancellationToken);

        if (existingDocs.Count > 0)
        {
            _logger.LogInformation("Restoring {Count} migrated documents into container '{ContainerName}'...", existingDocs.Count, containerName);
            foreach (var doc in existingDocs)
            {
                var campusId = doc["campusId"]?.ToString();
                if (string.IsNullOrWhiteSpace(campusId))
                {
                    campusId = "campus-ictu";
                    doc["campusId"] = campusId;
                }

                await newContainerResponse.Container.UpsertItemAsync(doc, new PartitionKey(campusId), cancellationToken: cancellationToken);
            }
            _logger.LogInformation("Successfully restored all {Count} documents into container '{ContainerName}'.", existingDocs.Count, containerName);
        }

        return newContainerResponse;
    }

    private static async Task EnsureSpatialAndCompositeIndexesAsync(Container container, ContainerProperties properties, CancellationToken cancellationToken)
    {
        var indexingPolicy = properties.IndexingPolicy ?? new IndexingPolicy();
        properties.IndexingPolicy = indexingPolicy;
        var modified = false;

        if (!indexingPolicy.SpatialIndexes.Any(index => index.Path.Equals("/location/*", StringComparison.Ordinal)))
        {
            indexingPolicy.SpatialIndexes.Add(new SpatialPath { Path = "/location/*" });
            modified = true;
        }

        if (!HasCompositeIndex(indexingPolicy, "/monthlyRentAmount", "/id"))
        {
            indexingPolicy.CompositeIndexes.Add(new System.Collections.ObjectModel.Collection<CompositePath>
            {
                new() { Path = "/monthlyRentAmount", Order = CompositePathSortOrder.Ascending },
                new() { Path = "/id", Order = CompositePathSortOrder.Ascending }
            });
            indexingPolicy.CompositeIndexes.Add(new System.Collections.ObjectModel.Collection<CompositePath>
            {
                new() { Path = "/monthlyRentAmount", Order = CompositePathSortOrder.Descending },
                new() { Path = "/id", Order = CompositePathSortOrder.Descending }
            });
            modified = true;
        }

        if (!HasCompositeIndex(indexingPolicy, "/areaSquareMeters", "/id"))
        {
            indexingPolicy.CompositeIndexes.Add(new System.Collections.ObjectModel.Collection<CompositePath>
            {
                new() { Path = "/areaSquareMeters", Order = CompositePathSortOrder.Ascending },
                new() { Path = "/id", Order = CompositePathSortOrder.Ascending }
            });
            indexingPolicy.CompositeIndexes.Add(new System.Collections.ObjectModel.Collection<CompositePath>
            {
                new() { Path = "/areaSquareMeters", Order = CompositePathSortOrder.Descending },
                new() { Path = "/id", Order = CompositePathSortOrder.Descending }
            });
            modified = true;
        }

        if (modified)
        {
            await container.ReplaceContainerAsync(properties, cancellationToken: cancellationToken);
        }
    }

    private static bool HasCompositeIndex(IndexingPolicy policy, string path1, string path2)
    {
        return policy.CompositeIndexes.Any(ci =>
            ci.Count >= 2 &&
            ci[0].Path.Equals(path1, StringComparison.Ordinal) &&
            ci[1].Path.Equals(path2, StringComparison.Ordinal));
    }

    private async Task SeedInitialDataAsync(Container campusesContainer, Container roomsContainer, CancellationToken cancellationToken)
    {
        // 1. Seed ICTU Campus
        const string ictuId = "campus-ictu";
        try
        {
            await campusesContainer.ReadItemAsync<CampusDocument>(ictuId, new PartitionKey(ictuId), cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            var ictu = new CampusDocument
            {
                Id = ictuId,
                Name = "Trường Đại học Công nghệ Thông tin và Truyền thông - ĐH Thái Nguyên (ICTU)",
                Code = "ICTU",
                Latitude = 21.5852,
                Longitude = 105.8074,
                Address = "Đường Z115, xã Quyết Thắng, TP. Thái Nguyên, Thái Nguyên"
            };

            await campusesContainer.UpsertItemAsync(ictu, new PartitionKey(ictu.Id), cancellationToken: cancellationToken);
            _logger.LogInformation("Seeded default campus: {CampusName}", ictu.Name);
        }

        // 2. Seed Sample Rooms near ICTU if none exist
        var countQuery = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.campusId = @campusId")
            .WithParameter("@campusId", ictuId);

        var iterator = roomsContainer.GetItemQueryIterator<int>(countQuery, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(ictuId) });
        var hasRooms = false;
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            if (response.FirstOrDefault() > 0)
            {
                hasRooms = true;
            }
        }

        if (!hasRooms)
        {
            var sampleRooms = new List<RoomListingDocument>
            {
                new()
                {
                    Id = "room-ictu-001",
                    CampusId = ictuId,
                    Title = "Phòng trọ khép kín cao cấp ngõ 89 Z115 sát cổng ICTU",
                    Description = "Phòng mới xây có điều hòa, nóng lạnh, gác xép, wifi tốc độ cao. Cách cổng chính ICTU 200m đi bộ.",
                    MonthlyRentAmount = 1800000,
                    MonthlyRentCurrency = "VND",
                    AreaSquareMeters = 22.0,
                    Address = "Số 15 ngõ 89 đường Z115, Quyết Thắng, TP. Thái Nguyên",
                    Latitude = 21.5865,
                    Longitude = 105.8080,
                    Location = new Microsoft.Azure.Cosmos.Spatial.Point(105.8080, 21.5865),
                    Status = 0, // Available
                    VerificationStatus = 2, // Verified
                    ThumbnailUrl = "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
                },
                new()
                {
                    Id = "room-ictu-002",
                    CampusId = ictuId,
                    Title = "Ký túc xá tư nhân full đồ ban công thoáng mát",
                    Description = "Full nội thất giường tầng, bàn học, tủ quần áo riêng. Giờ giấc tự do, an ninh camera 24/7.",
                    MonthlyRentAmount = 1200000,
                    MonthlyRentCurrency = "VND",
                    AreaSquareMeters = 18.5,
                    Address = "Đường Tân Thịnh, đối diện cổng phụ ICTU",
                    Latitude = 21.5840,
                    Longitude = 105.8060,
                    Location = new Microsoft.Azure.Cosmos.Spatial.Point(105.8060, 21.5840),
                    Status = 0, // Available
                    VerificationStatus = 2, // Verified
                    ThumbnailUrl = "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-5)
                },
                new()
                {
                    Id = "room-ictu-003",
                    CampusId = ictuId,
                    Title = "Căn hộ mini 1 phòng ngủ 1 khách cho nhóm sinh viên",
                    Description = "Căn hộ mini rộng rãi, có bếp riêng, máy giặt chung sân phơi tầng thượng. Thích hợp ở 2-3 bạn.",
                    MonthlyRentAmount = 2800000,
                    MonthlyRentCurrency = "VND",
                    AreaSquareMeters = 35.0,
                    Address = "Tổ 4, P. Thịnh Đán, TP. Thái Nguyên",
                    Latitude = 21.5790,
                    Longitude = 105.8020,
                    Location = new Microsoft.Azure.Cosmos.Spatial.Point(105.8020, 21.5790),
                    Status = 0, // Available
                    VerificationStatus = 2, // Verified
                    ThumbnailUrl = "https://images.unsplash.com/photo-1493809842364-78817add7ffb",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
                },
                new()
                {
                    Id = "room-ictu-004",
                    CampusId = ictuId,
                    Title = "Phòng trọ giá rẻ cho sinh viên năm nhất",
                    Description = "Phòng trọ bình dân có vệ sinh khép kín, gần chợ sinh viên Quyết Thắng.",
                    MonthlyRentAmount = 900000,
                    MonthlyRentCurrency = "VND",
                    AreaSquareMeters = 15.0,
                    Address = "Khu tập thể Z115, TP. Thái Nguyên",
                    Latitude = 21.5890,
                    Longitude = 105.8110,
                    Location = new Microsoft.Azure.Cosmos.Spatial.Point(105.8110, 21.5890),
                    Status = 0, // Available
                    VerificationStatus = 2, // Verified
                    ThumbnailUrl = "https://images.unsplash.com/photo-1598928506311-c55ded91a20c",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-7)
                },
                new()
                {
                    Id = "room-ictu-005",
                    CampusId = ictuId,
                    Title = "Phòng trọ đã có bạn thuê (Test Occupied - AC4)",
                    Description = "Phòng trọ đã kín người.",
                    MonthlyRentAmount = 1500000,
                    MonthlyRentCurrency = "VND",
                    AreaSquareMeters = 20.0,
                    Address = "Đường Z115, TP. Thái Nguyên",
                    Latitude = 21.5855,
                    Longitude = 105.8075,
                    Location = new Microsoft.Azure.Cosmos.Spatial.Point(105.8075, 21.5855),
                    Status = 1, // Occupied -> Must NOT be returned
                    VerificationStatus = 2, // Verified
                    ThumbnailUrl = null,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
                },
                new()
                {
                    Id = "room-ictu-006",
                    CampusId = ictuId,
                    Title = "Phòng trọ chưa kiểm duyệt (Test Unverified - AC4)",
                    Description = "Phòng mới đăng chờ admin duyệt.",
                    MonthlyRentAmount = 1600000,
                    MonthlyRentCurrency = "VND",
                    AreaSquareMeters = 20.0,
                    Address = "Đường Z115, TP. Thái Nguyên",
                    Latitude = 21.5855,
                    Longitude = 105.8075,
                    Location = new Microsoft.Azure.Cosmos.Spatial.Point(105.8075, 21.5855),
                    Status = 0, // Available
                    VerificationStatus = 0, // Unverified -> Must NOT be returned
                    ThumbnailUrl = null,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            };

            foreach (var room in sampleRooms)
            {
                await roomsContainer.UpsertItemAsync(room, new PartitionKey(room.CampusId), cancellationToken: cancellationToken);
            }

            _logger.LogInformation("Seeded {Count} sample rooms for campus {CampusId}.", sampleRooms.Count, ictuId);
        }
    }
}
