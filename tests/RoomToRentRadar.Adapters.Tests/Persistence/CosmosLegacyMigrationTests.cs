namespace RoomToRentRadar.Adapters.Tests.Persistence;

using System.Reflection;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Configurations;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

[TestFixture]
public class CosmosLegacyMigrationTests
{
    private Mock<CosmosClient> _mockClient = null!;
    private Mock<Database> _mockDatabase = null!;
    private Mock<Container> _mockLegacyContainer = null!;
    private Mock<Container> _mockListingsContainer = null!;
    private Mock<Container> _mockRoomDetailsContainer = null!;
    private Mock<Container> _mockCampusesContainer = null!;
    private CosmosDbOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _mockClient = new Mock<CosmosClient>();
        _mockDatabase = new Mock<Database>();
        _mockLegacyContainer = new Mock<Container>();
        _mockListingsContainer = new Mock<Container>();
        _mockRoomDetailsContainer = new Mock<Container>();
        _mockCampusesContainer = new Mock<Container>();

        _options = new CosmosDbOptions
        {
            DatabaseName = "TestDb",
            ContainerName = "RoomListings",
            RoomDetailsContainerName = "RoomDetails",
            CampusesContainerName = "Campuses",
            SeedSampleData = false
        };

        // Database response mock
        var mockDbResponse = new Mock<DatabaseResponse>();
        mockDbResponse.Setup(r => r.Database).Returns(_mockDatabase.Object);
        _mockClient.Setup(c => c.CreateDatabaseIfNotExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDbResponse.Object);

        // Campuses container response mock
        var mockCampusesResponse = new Mock<ContainerResponse>();
        mockCampusesResponse.Setup(r => r.Container).Returns(_mockCampusesContainer.Object);
        _mockDatabase.Setup(d => d.CreateContainerIfNotExistsAsync(It.Is<ContainerProperties>(p => p.Id == "Campuses"), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCampusesResponse.Object);

        // RoomDetails container response mock
        var mockDetailsResponse = new Mock<ContainerResponse>();
        mockDetailsResponse.Setup(r => r.Container).Returns(_mockRoomDetailsContainer.Object);
        _mockDatabase.Setup(d => d.CreateContainerIfNotExistsAsync(It.Is<ContainerProperties>(p => p.Id == "RoomDetails"), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDetailsResponse.Object);

        // RoomListings container response mock
        var mockListingsResponse = new Mock<ContainerResponse>();
        mockListingsResponse.Setup(r => r.Container).Returns(_mockListingsContainer.Object);
        var listingProps = new ContainerProperties("RoomListings", "/campusId");
        mockListingsResponse.Setup(r => r.Resource).Returns(listingProps);
        _mockDatabase.Setup(d => d.CreateContainerIfNotExistsAsync(It.Is<ContainerProperties>(p => p.Id == "RoomListings"), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockListingsResponse.Object);

        _mockDatabase.Setup(d => d.GetContainer("Rooms")).Returns(_mockLegacyContainer.Object);

        // Default: legacy container exists
        var mockLegacyPropsResponse = new Mock<ContainerResponse>();
        mockLegacyPropsResponse.Setup(r => r.Resource).Returns(new ContainerProperties("Rooms", "/id"));
        _mockLegacyContainer.Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockLegacyPropsResponse.Object);

        // Default: migration status document not yet completed (404 NotFound)
        _mockRoomDetailsContainer
            .Setup(c => c.ReadItemAsync<JObject>("__migration_legacy_rooms_status", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Not found", System.Net.HttpStatusCode.NotFound, 0, "1", 0));

        // Default: point reads return 404 NotFound
        _mockRoomDetailsContainer
            .Setup(c => c.ReadItemAsync<JObject>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Not found", System.Net.HttpStatusCode.NotFound, 0, "1", 0));

        _mockListingsContainer
            .Setup(c => c.ReadItemAsync<RoomListingDocument>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Not found", System.Net.HttpStatusCode.NotFound, 0, "1", 0));
    }

    [Test]
    public async Task SynchronizeRoomDetailsAsync_WhenDetailHasNewerTombstone_DoesNotReviveIt()
    {
        var listing = new RoomListingDocument
        {
            Id = "room-deleted",
            CampusId = "campus-ictu",
            ProjectionVersion = 10,
            Status = 0
        };
        var page = new Mock<FeedResponse<RoomListingDocument>>();
        page.Setup(response => response.GetEnumerator()).Returns(new List<RoomListingDocument> { listing }.GetEnumerator());
        var iterator = new Mock<FeedIterator<RoomListingDocument>>();
        iterator.SetupSequence(value => value.HasMoreResults).Returns(true).Returns(false);
        iterator.Setup(value => value.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(page.Object);
        _mockListingsContainer.Setup(container => container.GetItemQueryIterator<RoomListingDocument>(
                It.IsAny<QueryDefinition>(), null, null))
            .Returns(iterator.Object);

        var readResponse = new Mock<ItemResponse<RoomDocument>>();
        readResponse.SetupGet(response => response.Resource).Returns(new RoomDocument
        {
            Id = "room-deleted",
            IsDeleted = true,
            ProjectionVersion = 11
        });
        _mockRoomDetailsContainer.Setup(container => container.ReadItemAsync<RoomDocument>(
                "room-deleted", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(readResponse.Object);

        var initializer = new CosmosDbInitializer(
            _mockClient.Object,
            Options.Create(_options),
            NullLogger<CosmosDbInitializer>.Instance);
        var method = typeof(CosmosDbInitializer).GetMethod(
            "SynchronizeRoomDetailsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)method.Invoke(initializer, [_mockListingsContainer.Object, _mockRoomDetailsContainer.Object, CancellationToken.None])!;

        _mockRoomDetailsContainer.Verify(container => container.CreateItemAsync(
            It.IsAny<RoomDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRoomDetailsContainer.Verify(container => container.ReplaceItemAsync(
            It.IsAny<RoomDocument>(), It.IsAny<string>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task InitializeAsync_WhenLegacyRecordLacksCoordinates_ShouldPreserveInRoomDetails_AndExcludeFromRoomListings()
    {
        // Document has price & campusId, but lacks latitude & longitude
        var legacyDoc = new JObject
        {
            ["id"] = "legacy-no-coord-1",
            ["campusId"] = "campus-ictu",
            ["price"] = 2000000m,
            ["area"] = 25.0
            // latitude and longitude are missing!
        };

        SetupLegacyDocuments([legacyDoc]);

        var capturedDetails = new List<JObject>();
        _mockRoomDetailsContainer
            .Setup(c => c.UpsertItemAsync(It.IsAny<JObject>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .Callback<JObject, PartitionKey?, ItemRequestOptions, CancellationToken>((doc, pk, opt, ct) => capturedDetails.Add(doc))
            .ReturnsAsync(new Mock<ItemResponse<JObject>>().Object);

        var capturedListings = new List<RoomListingDocument>();
        _mockListingsContainer
            .Setup(c => c.UpsertItemAsync(It.IsAny<RoomListingDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .Callback<RoomListingDocument, PartitionKey?, ItemRequestOptions, CancellationToken>((doc, pk, opt, ct) => capturedListings.Add(doc))
            .ReturnsAsync(new Mock<ItemResponse<RoomListingDocument>>().Object);

        var initializer = new CosmosDbInitializer(
            _mockClient.Object,
            Options.Create(_options),
            NullLogger<CosmosDbInitializer>.Instance);

        await initializer.InitializeAsync();

        // Verification: preserved in RoomDetails, but excluded from RoomListings
        var capturedRooms = capturedDetails.Where(d => d["id"]?.ToString() != "__migration_legacy_rooms_status").ToList();
        var statusDoc = capturedDetails.FirstOrDefault(d => d["id"]?.ToString() == "__migration_legacy_rooms_status");

        Assert.Multiple(() =>
        {
            Assert.That(capturedRooms, Has.Count.EqualTo(1));
            Assert.That(capturedRooms[0]["id"]?.ToString(), Is.EqualTo("legacy-no-coord-1"));
            Assert.That(capturedListings, Is.Empty, "Ineligible legacy record without coordinates must NOT be added to RoomListings");
            Assert.That(statusDoc, Is.Not.Null, "Migration completion status document must be recorded");
            Assert.That(statusDoc!["status"]?.ToString(), Is.EqualTo("Completed"));
        });
    }

    [Test]
    public async Task InitializeAsync_WhenLegacyRecordLacksCampusId_ShouldPreserveInRoomDetails_AndExcludeFromRoomListings()
    {
        // Document has coords, but lacks campusId
        var legacyDoc = new JObject
        {
            ["id"] = "legacy-no-campus-2",
            ["price"] = 1800000m,
            ["area"] = 20.0,
            ["latitude"] = 21.5852,
            ["longitude"] = 105.8074
            // campusId is missing! Must NOT default to "campus-ictu"
        };

        SetupLegacyDocuments([legacyDoc]);

        var capturedDetails = new List<JObject>();
        _mockRoomDetailsContainer
            .Setup(c => c.UpsertItemAsync(It.IsAny<JObject>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .Callback<JObject, PartitionKey?, ItemRequestOptions, CancellationToken>((doc, pk, opt, ct) => capturedDetails.Add(doc))
            .ReturnsAsync(new Mock<ItemResponse<JObject>>().Object);

        var capturedListings = new List<RoomListingDocument>();
        _mockListingsContainer
            .Setup(c => c.UpsertItemAsync(It.IsAny<RoomListingDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .Callback<RoomListingDocument, PartitionKey?, ItemRequestOptions, CancellationToken>((doc, pk, opt, ct) => capturedListings.Add(doc))
            .ReturnsAsync(new Mock<ItemResponse<RoomListingDocument>>().Object);

        var initializer = new CosmosDbInitializer(
            _mockClient.Object,
            Options.Create(_options),
            NullLogger<CosmosDbInitializer>.Instance);

        await initializer.InitializeAsync();

        var capturedRooms = capturedDetails.Where(d => d["id"]?.ToString() != "__migration_legacy_rooms_status").ToList();
        Assert.Multiple(() =>
        {
            Assert.That(capturedRooms, Has.Count.EqualTo(1));
            Assert.That(capturedRooms[0]["id"]?.ToString(), Is.EqualTo("legacy-no-campus-2"));
            Assert.That(capturedListings, Is.Empty, "Ineligible legacy record without campusId must NOT be fabricated into RoomListings");
        });
    }

    [Test]
    public async Task InitializeAsync_WhenLegacyRecordIsValid_ShouldMigrateToBothRoomDetailsAndRoomListings()
    {
        var legacyDoc = new JObject
        {
            ["id"] = "valid-room-100",
            ["campusId"] = "campus-ictu",
            ["title"] = "Phòng trọ đẹp Z115",
            ["price"] = 2500000m,
            ["area"] = 28.0,
            ["isVerified"] = true,
            ["latitude"] = 21.5860,
            ["longitude"] = 105.8080
        };

        SetupLegacyDocuments([legacyDoc]);

        var capturedDetails = new List<JObject>();
        _mockRoomDetailsContainer
            .Setup(c => c.UpsertItemAsync(It.IsAny<JObject>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .Callback<JObject, PartitionKey?, ItemRequestOptions, CancellationToken>((doc, pk, opt, ct) => capturedDetails.Add(doc))
            .ReturnsAsync(new Mock<ItemResponse<JObject>>().Object);

        var capturedListings = new List<RoomListingDocument>();
        _mockListingsContainer
            .Setup(c => c.UpsertItemAsync(It.IsAny<RoomListingDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .Callback<RoomListingDocument, PartitionKey?, ItemRequestOptions, CancellationToken>((doc, pk, opt, ct) => capturedListings.Add(doc))
            .ReturnsAsync(new Mock<ItemResponse<RoomListingDocument>>().Object);

        var initializer = new CosmosDbInitializer(
            _mockClient.Object,
            Options.Create(_options),
            NullLogger<CosmosDbInitializer>.Instance);

        await initializer.InitializeAsync();

        var capturedRooms = capturedDetails.Where(d => d["id"]?.ToString() != "__migration_legacy_rooms_status").ToList();
        Assert.Multiple(() =>
        {
            Assert.That(capturedRooms, Has.Count.EqualTo(1));
            Assert.That(capturedListings, Has.Count.EqualTo(1));

            var listing = capturedListings[0];
            Assert.That(listing.Id, Is.EqualTo("valid-room-100"));
            Assert.That(listing.CampusId, Is.EqualTo("campus-ictu"));
            Assert.That(listing.MonthlyRentAmount, Is.EqualTo(2500000m));
            Assert.That(listing.AreaSquareMeters, Is.EqualTo(28.0));
            Assert.That(listing.VerificationStatus, Is.EqualTo(2), "isVerified:true must map to verificationStatus:2 (Verified)");
            Assert.That(listing.Location.Position.Latitude, Is.EqualTo(21.5860));
            Assert.That(listing.Location.Position.Longitude, Is.EqualTo(105.8080));
        });
    }

    [Test]
    public async Task InitializeAsync_WhenLegacyRecordsUseKnownStatuses_PreservesEachStatusInListings()
    {
        var statuses = new[] { 0, 1, 2, 3 };
        var legacyDocuments = statuses.Select(status => new JObject
        {
            ["id"] = $"legacy-status-{status}",
            ["campusId"] = "campus-ictu",
            ["price"] = 2_000_000m,
            ["area"] = 25.0,
            ["latitude"] = 21.5852,
            ["longitude"] = 105.8074,
            ["status"] = status
        }).ToList();
        SetupLegacyDocuments(legacyDocuments);

        var capturedListings = new List<RoomListingDocument>();
        _mockListingsContainer
            .Setup(c => c.UpsertItemAsync(It.IsAny<RoomListingDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .Callback<RoomListingDocument, PartitionKey?, ItemRequestOptions, CancellationToken>((document, _, _, _) => capturedListings.Add(document))
            .ReturnsAsync(new Mock<ItemResponse<RoomListingDocument>>().Object);

        var initializer = new CosmosDbInitializer(
            _mockClient.Object,
            Options.Create(_options),
            NullLogger<CosmosDbInitializer>.Instance);

        await initializer.InitializeAsync();

        Assert.That(capturedListings.ToDictionary(listing => listing.Id, listing => listing.Status), Is.EqualTo(
            new Dictionary<string, int>
            {
                ["legacy-status-0"] = 0,
                ["legacy-status-1"] = 1,
                ["legacy-status-2"] = 2,
                ["legacy-status-3"] = 3
            }));
    }

    [Test]
    public async Task InitializeAsync_WhenAlreadyMigrated_ShouldBeIdempotentAndNotReWrite()
    {
        var legacyDoc = new JObject
        {
            ["id"] = "existing-room-200",
            ["campusId"] = "campus-ictu",
            ["price"] = 2000000m,
            ["area"] = 20.0,
            ["latitude"] = 21.5852,
            ["longitude"] = 105.8074,
            ["status"] = 1
        };

        SetupLegacyDocuments([legacyDoc]);

        // Target containers already contain matching document (data parity verified)
        SetupExistingDetail(_mockRoomDetailsContainer, "existing-room-200", legacyDoc);
        SetupExistingListing(_mockListingsContainer, "existing-room-200", "campus-ictu", new RoomListingDocument
        {
            Id = "existing-room-200",
            CampusId = "campus-ictu",
            Title = "Phòng trọ existing-room-200",
            MonthlyRentAmount = 2000000m,
            AreaSquareMeters = 20.0,
            Latitude = 21.5852,
            Longitude = 105.8074,
            Status = 1,
            VerificationStatus = 0
        });

        var initializer = new CosmosDbInitializer(
            _mockClient.Object,
            Options.Create(_options),
            NullLogger<CosmosDbInitializer>.Instance);

        await initializer.InitializeAsync();

        // Neither container should be written to again for legacy document with identical data
        _mockRoomDetailsContainer.Verify(c => c.UpsertItemAsync(It.Is<JObject>(d => d["id"] != null && d["id"]!.ToString() == "existing-room-200"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockListingsContainer.Verify(c => c.UpsertItemAsync(It.Is<RoomListingDocument>(d => d.Id == "existing-room-200"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task InitializeAsync_WhenAlreadyMigrated_WithDataDiscrepancy_ShouldReconcileAndUpdateTargetContainers()
    {
        var legacyDoc = new JObject
        {
            ["id"] = "discrepant-room-300",
            ["campusId"] = "campus-ictu",
            ["price"] = 2500000m, // Source of truth has 2.5m
            ["area"] = 25.0,
            ["latitude"] = 21.5852,
            ["longitude"] = 105.8074
        };

        SetupLegacyDocuments([legacyDoc]);

        // Target containers contain stale data (e.g. price was 1.8m)
        var staleDetailDoc = new JObject
        {
            ["id"] = "discrepant-room-300",
            ["campusId"] = "campus-ictu",
            ["price"] = 1800000m,
            ["area"] = 25.0,
            ["latitude"] = 21.5852,
            ["longitude"] = 105.8074
        };
        SetupExistingDetail(_mockRoomDetailsContainer, "discrepant-room-300", staleDetailDoc);

        var staleListingDoc = new RoomListingDocument
        {
            Id = "discrepant-room-300",
            CampusId = "campus-ictu",
            Title = "Phòng trọ discrepant-room-300",
            MonthlyRentAmount = 1800000m, // Stale price
            AreaSquareMeters = 25.0,
            Latitude = 21.5852,
            Longitude = 105.8074,
            Status = 1,
            VerificationStatus = 0
        };
        SetupExistingListing(_mockListingsContainer, "discrepant-room-300", "campus-ictu", staleListingDoc);

        var capturedDetails = new List<JObject>();
        _mockRoomDetailsContainer
            .Setup(c => c.UpsertItemAsync(It.IsAny<JObject>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .Callback<JObject, PartitionKey?, ItemRequestOptions, CancellationToken>((doc, pk, opt, ct) => capturedDetails.Add(doc))
            .ReturnsAsync(new Mock<ItemResponse<JObject>>().Object);

        var capturedListings = new List<RoomListingDocument>();
        _mockListingsContainer
            .Setup(c => c.UpsertItemAsync(It.IsAny<RoomListingDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .Callback<RoomListingDocument, PartitionKey?, ItemRequestOptions, CancellationToken>((doc, pk, opt, ct) => capturedListings.Add(doc))
            .ReturnsAsync(new Mock<ItemResponse<RoomListingDocument>>().Object);

        var initializer = new CosmosDbInitializer(
            _mockClient.Object,
            Options.Create(_options),
            NullLogger<CosmosDbInitializer>.Instance);

        await initializer.InitializeAsync();

        // Both containers MUST be updated to reconcile the data discrepancy
        _mockRoomDetailsContainer.Verify(c => c.UpsertItemAsync(It.Is<JObject>(d => d["id"] != null && d["id"]!.ToString() == "discrepant-room-300"), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockListingsContainer.Verify(c => c.UpsertItemAsync(It.Is<RoomListingDocument>(d => d.Id == "discrepant-room-300" && d.MonthlyRentAmount == 2500000m), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task InitializeAsync_WhenMigrationAlreadyCompletedInMetadata_ShouldSkipMigrationImmediatelyWithoutQueryingLegacy()
    {
        var completedMetadata = new JObject
        {
            ["id"] = "__migration_legacy_rooms_status",
            ["status"] = "Completed"
        };
        var mockResponse = new Mock<ItemResponse<JObject>>();
        mockResponse.Setup(r => r.Resource).Returns(completedMetadata);

        _mockRoomDetailsContainer
            .Setup(c => c.ReadItemAsync<JObject>("__migration_legacy_rooms_status", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        var initializer = new CosmosDbInitializer(
            _mockClient.Object,
            Options.Create(_options),
            NullLogger<CosmosDbInitializer>.Instance);

        await initializer.InitializeAsync();

        // Must not query legacy container or target containers at all
        _mockLegacyContainer.Verify(c => c.GetItemQueryIterator<JObject>(It.IsAny<QueryDefinition>(), null, null), Times.Never);
        _mockRoomDetailsContainer.Verify(c => c.UpsertItemAsync(It.IsAny<JObject>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static void SetupExistingDetail(Mock<Container> containerMock, string id, JObject document)
    {
        var mockResponse = new Mock<ItemResponse<JObject>>();
        mockResponse.Setup(r => r.Resource).Returns(document);
        containerMock
            .Setup(c => c.ReadItemAsync<JObject>(id, new PartitionKey(id), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);
    }

    private static void SetupExistingListing(Mock<Container> containerMock, string id, string campusId, RoomListingDocument document)
    {
        var mockResponse = new Mock<ItemResponse<RoomListingDocument>>();
        mockResponse.Setup(r => r.Resource).Returns(document);
        containerMock
            .Setup(c => c.ReadItemAsync<RoomListingDocument>(id, new PartitionKey(campusId), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);
    }

    private void SetupLegacyDocuments(List<JObject> documents)
    {
        var mockIterator = new Mock<FeedIterator<JObject>>();
        var readOnce = false;

        mockIterator.Setup(i => i.HasMoreResults).Returns(() => !readOnce);
        mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                readOnce = true;
                var mockResponse = new Mock<FeedResponse<JObject>>();
                mockResponse.Setup(r => r.GetEnumerator()).Returns(documents.GetEnumerator());
                mockResponse.Setup(r => r.Resource).Returns(documents);
                return mockResponse.Object;
            });

        _mockLegacyContainer
            .Setup(c => c.GetItemQueryIterator<JObject>(It.IsAny<QueryDefinition>(), null, null))
            .Returns(mockIterator.Object);
    }

    private static void SetupQueryIds(Mock<Container> containerMock, List<string> existingIds)
    {
        var docs = existingIds.Select(id => new JObject { ["id"] = id }).ToList();
        var mockIterator = new Mock<FeedIterator<JObject>>();
        var readOnce = false;

        mockIterator.Setup(i => i.HasMoreResults).Returns(() => !readOnce);
        mockIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                readOnce = true;
                var mockResponse = new Mock<FeedResponse<JObject>>();
                mockResponse.Setup(r => r.GetEnumerator()).Returns(docs.GetEnumerator());
                mockResponse.Setup(r => r.Resource).Returns(docs);
                return mockResponse.Object;
            });

        containerMock
            .Setup(c => c.GetItemQueryIterator<JObject>(It.Is<QueryDefinition>(q => q.QueryText == "SELECT c.id FROM c"), null, null))
            .Returns(mockIterator.Object);
    }
}
