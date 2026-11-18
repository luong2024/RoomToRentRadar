namespace RoomToRentRadar.Adapters.Tests.Persistence;

using System.Reflection;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Configurations;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

[TestFixture]
public class CosmosStartupSynchronizationTests
{
    private Mock<CosmosClient> _mockClient = null!;
    private Mock<Container> _mockListingsContainer = null!;
    private Mock<Container> _mockRoomDetailsContainer = null!;
    private CosmosDbInitializer _initializer = null!;
    private MethodInfo _synchronizeMethod = null!;

    [SetUp]
    public void SetUp()
    {
        _mockClient = new Mock<CosmosClient>();
        _mockListingsContainer = new Mock<Container>();
        _mockRoomDetailsContainer = new Mock<Container>();

        var options = new CosmosDbOptions
        {
            DatabaseName = "TestDb",
            ContainerName = "RoomListings",
            RoomDetailsContainerName = "RoomDetails",
            CampusesContainerName = "Campuses",
            SeedSampleData = false
        };

        _initializer = new CosmosDbInitializer(
            _mockClient.Object,
            Options.Create(options),
            NullLogger<CosmosDbInitializer>.Instance);

        _synchronizeMethod = typeof(CosmosDbInitializer).GetMethod(
            "SynchronizeRoomDetailsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
    }

    private void SetupListings(params RoomListingDocument[] listings)
    {
        var page = new Mock<FeedResponse<RoomListingDocument>>();
        page.Setup(response => response.GetEnumerator()).Returns(((IEnumerable<RoomListingDocument>)listings).GetEnumerator());

        var iterator = new Mock<FeedIterator<RoomListingDocument>>();
        iterator.SetupSequence(value => value.HasMoreResults).Returns(true).Returns(false);
        iterator.Setup(value => value.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(page.Object);

        _mockListingsContainer.Setup(container => container.GetItemQueryIterator<RoomListingDocument>(
                It.IsAny<QueryDefinition>(), null, null))
            .Returns(iterator.Object);
    }

    private void SetupExistingDetail(RoomDocument? detail)
    {
        if (detail is null)
        {
            _mockRoomDetailsContainer.Setup(container => container.ReadItemAsync<RoomDocument>(
                    It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("Not found", System.Net.HttpStatusCode.NotFound, 0, "1", 0));
        }
        else
        {
            var readResponse = new Mock<ItemResponse<RoomDocument>>();
            readResponse.SetupGet(response => response.Resource).Returns(detail);
            _mockRoomDetailsContainer.Setup(container => container.ReadItemAsync<RoomDocument>(
                    detail.Id, It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(readResponse.Object);
        }
    }

    [Test]
    public async Task Synchronize_WhenDetailIsDeleted_DoesNotOverwriteOrReviveTombstone()
    {
        // 1. Phòng đã xóa không xuất hiện trở lại
        var listing = new RoomListingDocument
        {
            Id = "room-deleted",
            CampusId = "campus-ictu",
            Title = "Active Listing",
            ProjectionVersion = 10,
            Status = 0
        };
        SetupListings(listing);

        var deletedDetail = new RoomDocument
        {
            Id = "room-deleted",
            Title = "Old Detail",
            IsDeleted = true,
            ProjectionVersion = 10
        };
        SetupExistingDetail(deletedDetail);

        await (Task)_synchronizeMethod.Invoke(_initializer, [_mockListingsContainer.Object, _mockRoomDetailsContainer.Object, CancellationToken.None])!;

        _mockRoomDetailsContainer.Verify(c => c.CreateItemAsync(
            It.IsAny<RoomDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRoomDetailsContainer.Verify(c => c.ReplaceItemAsync(
            It.IsAny<RoomDocument>(), It.IsAny<string>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Synchronize_WhenDetailHasNewerProjectionVersion_DoesNotOverwriteWithStaleListing()
    {
        // 2. Projection mới hơn không bị ghi đè bởi dữ liệu cũ
        var listing = new RoomListingDocument
        {
            Id = "room-stale",
            CampusId = "campus-ictu",
            Title = "Stale Title",
            ProjectionVersion = 10,
            Status = 0
        };
        SetupListings(listing);

        var newerDetail = new RoomDocument
        {
            Id = "room-stale",
            Title = "Newer Title",
            ProjectionVersion = 12,
            IsDeleted = false
        };
        SetupExistingDetail(newerDetail);

        await (Task)_synchronizeMethod.Invoke(_initializer, [_mockListingsContainer.Object, _mockRoomDetailsContainer.Object, CancellationToken.None])!;

        _mockRoomDetailsContainer.Verify(c => c.ReplaceItemAsync(
            It.IsAny<RoomDocument>(), It.IsAny<string>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Synchronize_WhenDetailHasSameVersionAndMatchingContent_SkipsRedundantWrite()
    {
        // 3. Chạy startup synchronization nhiều lần không làm sai dữ liệu và không ghi lại bản ghi có cùng version
        var listing = new RoomListingDocument
        {
            Id = "room-matched",
            CampusId = "campus-ictu",
            Title = "Cozy Studio",
            Description = "Near university",
            MonthlyRentAmount = 2_000_000m,
            AreaSquareMeters = 25.0,
            Address = "123 Street",
            ThumbnailUrl = "https://img.test/thumb.jpg",
            VerificationStatus = 2,
            Status = 0,
            Latitude = 21.5,
            Longitude = 105.8,
            ProjectionVersion = 10
        };
        SetupListings(listing);

        var matchingDetail = new RoomDocument
        {
            Id = "room-matched",
            Title = "Cozy Studio",
            Description = "Near university",
            Price = 2_000_000m,
            Area = 25.0,
            Address = "123 Street",
            ThumbnailUrl = "https://img.test/thumb.jpg",
            IsVerified = true,
            Status = 0,
            Latitude = 21.5,
            Longitude = 105.8,
            ProjectionVersion = 10,
            IsDeleted = false,
            Amenities = ["Wifi", "Aircon"],
            HouseRules = ["No smoking"]
        };
        SetupExistingDetail(matchingDetail);

        await (Task)_synchronizeMethod.Invoke(_initializer, [_mockListingsContainer.Object, _mockRoomDetailsContainer.Object, CancellationToken.None])!;

        // Must NOT issue redundant ReplaceItemAsync write to CosmosDB
        _mockRoomDetailsContainer.Verify(c => c.ReplaceItemAsync(
            It.IsAny<RoomDocument>(), It.IsAny<string>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Synchronize_WhenDetailHasSameVersionButDivergedContent_ReconcilesFromListingAuthoritativeSource()
    {
        // 4. Khi dữ liệu listing và detail có cùng version nhưng khác nội dung, hệ thống xử lý theo quy tắc đã xác định (reconcile)
        var listing = new RoomListingDocument
        {
            Id = "room-diverged",
            CampusId = "campus-ictu",
            Title = "Corrected Title from Listing",
            Description = "Updated Description",
            MonthlyRentAmount = 2_500_000m,
            AreaSquareMeters = 28.0,
            Address = "Updated Address",
            ThumbnailUrl = "https://img.test/new-thumb.jpg",
            VerificationStatus = 2,
            Status = 0,
            Latitude = 21.58,
            Longitude = 105.81,
            ProjectionVersion = 10
        };
        SetupListings(listing);

        var divergedDetail = new RoomDocument
        {
            Id = "room-diverged",
            Title = "Outdated Title in Detail",
            Description = "Old Description",
            Price = 2_000_000m,
            Area = 25.0,
            Address = "Old Address",
            ThumbnailUrl = "https://img.test/old-thumb.jpg",
            IsVerified = false,
            Status = 0,
            Latitude = 21.50,
            Longitude = 105.80,
            ProjectionVersion = 10,
            IsDeleted = false,
            Amenities = ["Wifi", "Fridge"],
            HouseRules = ["No pets"],
            ETag = "\"etag-detail-1\""
        };
        SetupExistingDetail(divergedDetail);

        RoomDocument? replacedDetail = null;
        _mockRoomDetailsContainer.Setup(c => c.ReplaceItemAsync(
                It.IsAny<RoomDocument>(),
                "room-diverged",
                It.IsAny<PartitionKey?>(),
                It.Is<ItemRequestOptions>(opt => opt.IfMatchEtag == "\"etag-detail-1\""),
                It.IsAny<CancellationToken>()))
            .Callback<RoomDocument, string, PartitionKey?, ItemRequestOptions, CancellationToken>((doc, id, pk, opt, ct) => replacedDetail = doc)
            .ReturnsAsync(new Mock<ItemResponse<RoomDocument>>().Object);

        await (Task)_synchronizeMethod.Invoke(_initializer, [_mockListingsContainer.Object, _mockRoomDetailsContainer.Object, CancellationToken.None])!;

        Assert.That(replacedDetail, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(replacedDetail!.Title, Is.EqualTo("Corrected Title from Listing"));
            Assert.That(replacedDetail.Price, Is.EqualTo(2_500_000m));
            Assert.That(replacedDetail.Area, Is.EqualTo(28.0));
            Assert.That(replacedDetail.Address, Is.EqualTo("Updated Address"));
            Assert.That(replacedDetail.IsVerified, Is.True);
            // Detail-only fields must be preserved during reconciliation
            Assert.That(replacedDetail.Amenities, Is.EqualTo(new[] { "Wifi", "Fridge" }));
            Assert.That(replacedDetail.HouseRules, Is.EqualTo(new[] { "No pets" }));
        });
    }

    [Test]
    public async Task Synchronize_WhenConcurrentUpdateCausesPreconditionFailed_SkipsGracefullyWithoutFailing()
    {
        // 5. Reconciliation hoạt động đúng khi có cập nhật song song (ETag conflict)
        var listing = new RoomListingDocument
        {
            Id = "room-conflict",
            CampusId = "campus-ictu",
            Title = "Updated Title",
            ProjectionVersion = 11,
            Status = 0
        };
        SetupListings(listing);

        var existingDetail = new RoomDocument
        {
            Id = "room-conflict",
            Title = "Old Title",
            ProjectionVersion = 10,
            IsDeleted = false,
            ETag = "\"etag-old\""
        };
        SetupExistingDetail(existingDetail);

        _mockRoomDetailsContainer.Setup(c => c.ReplaceItemAsync(
                It.IsAny<RoomDocument>(),
                "room-conflict",
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("ETag precondition failed", System.Net.HttpStatusCode.PreconditionFailed, 0, "1", 0));

        // Must complete gracefully without throwing CosmosException
        Assert.DoesNotThrowAsync(async () =>
        {
            await (Task)_synchronizeMethod.Invoke(_initializer, [_mockListingsContainer.Object, _mockRoomDetailsContainer.Object, CancellationToken.None])!;
        });
    }

    [Test]
    public async Task Synchronize_WhenDetailHasOlderVersion_UpdatesDetailAndPreservesDetailOnlyFields()
    {
        var listing = new RoomListingDocument
        {
            Id = "room-older",
            CampusId = "campus-ictu",
            Title = "Brand New Title",
            MonthlyRentAmount = 3_000_000m,
            AreaSquareMeters = 30.0,
            Address = "321 New Road",
            ThumbnailUrl = "https://img.test/new.jpg",
            VerificationStatus = 2,
            Status = 0,
            Latitude = 21.6,
            Longitude = 105.9,
            ProjectionVersion = 15
        };
        SetupListings(listing);

        var olderDetail = new RoomDocument
        {
            Id = "room-older",
            Title = "Old Title",
            Price = 2_000_000m,
            Area = 20.0,
            Address = "Old Road",
            ThumbnailUrl = "https://img.test/old.jpg",
            IsVerified = false,
            Status = 0,
            ProjectionVersion = 10,
            Amenities = ["Washing machine", "Balcony"],
            HouseRules = ["No smoking"],
            LandlordPhone = "0912345678",
            ETag = "\"etag-older\""
        };
        SetupExistingDetail(olderDetail);

        RoomDocument? updatedDoc = null;
        _mockRoomDetailsContainer.Setup(c => c.ReplaceItemAsync(
                It.IsAny<RoomDocument>(),
                "room-older",
                It.IsAny<PartitionKey?>(),
                It.Is<ItemRequestOptions>(opt => opt.IfMatchEtag == "\"etag-older\""),
                It.IsAny<CancellationToken>()))
            .Callback<RoomDocument, string, PartitionKey?, ItemRequestOptions, CancellationToken>((doc, id, pk, opt, ct) => updatedDoc = doc)
            .ReturnsAsync(new Mock<ItemResponse<RoomDocument>>().Object);

        await (Task)_synchronizeMethod.Invoke(_initializer, [_mockListingsContainer.Object, _mockRoomDetailsContainer.Object, CancellationToken.None])!;

        Assert.That(updatedDoc, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(updatedDoc!.Title, Is.EqualTo("Brand New Title"));
            Assert.That(updatedDoc.Price, Is.EqualTo(3_000_000m));
            Assert.That(updatedDoc.ProjectionVersion, Is.EqualTo(15));
            Assert.That(updatedDoc.Amenities, Is.EqualTo(new[] { "Washing machine", "Balcony" }));
            Assert.That(updatedDoc.HouseRules, Is.EqualTo(new[] { "No smoking" }));
            Assert.That(updatedDoc.LandlordPhone, Is.EqualTo("0912345678"));
        });
    }
}
