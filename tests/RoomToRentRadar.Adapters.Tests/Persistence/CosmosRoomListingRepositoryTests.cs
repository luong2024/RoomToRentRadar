using System.Net;
using Microsoft.Azure.Cosmos;
using Moq;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Mappings;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;
using ListingRoomStatus = RoomToRentRadar.Domain.Enums.RoomStatus;
using RoomToRentRadar.Application.DTOs;

namespace RoomToRentRadar.Adapters.Tests.Persistence;

[TestFixture]
public class CosmosRoomListingRepositoryTests
{
    [Test]
    public async Task HideAsync_PatchesVisibilityInEachCampusWithoutRewritingDetails()
    {
        var listingsContainer = new Mock<Container>();
        var roomsContainer = new Mock<Container>(MockBehavior.Strict);
        var response = new Mock<FeedResponse<string>>();
        response.Setup(item => item.GetEnumerator()).Returns(() =>
            new List<string> { "campus-a", "campus-b" }.GetEnumerator());
        var iterator = new Mock<FeedIterator<string>>();
        iterator.SetupSequence(item => item.HasMoreResults).Returns(true).Returns(false);
        iterator.Setup(item => item.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(response.Object);
        QueryDefinition? capturedQuery = null;
        listingsContainer
            .Setup(container => container.GetItemQueryIterator<string>(It.IsAny<QueryDefinition>(), null, null))
            .Callback<QueryDefinition, string, QueryRequestOptions>((query, _, _) => capturedQuery = query)
            .Returns(iterator.Object);
        listingsContainer
            .Setup(container => container.PatchItemAsync<RoomListingDocument>(
                "room-1", It.IsAny<PartitionKey>(), It.IsAny<IReadOnlyList<PatchOperation>>(),
                It.IsAny<PatchItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomListingDocument>>().Object);

        var repository = new CosmosRoomListingRepository(
            listingsContainer.Object, roomsContainer.Object, new Mock<Container>(MockBehavior.Strict).Object);

        var result = await repository.HideAsync("room-1");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EquivalentTo(new[] { "campus-a", "campus-b" }));
            Assert.That(capturedQuery!.QueryText, Does.Contain("c.id = @id"));
            Assert.That(capturedQuery.GetQueryParameters().Single(parameter => parameter.Name == "@id").Value, Is.EqualTo("room-1"));
        });

        foreach (var campusId in result)
        {
            listingsContainer.Verify(container => container.PatchItemAsync<RoomListingDocument>(
                "room-1", new PartitionKey(campusId),
                It.Is<IReadOnlyList<PatchOperation>>(patch => patch.Select(operation => operation.Path)
                    .SequenceEqual(new[] { "/status", "/verificationStatus" })),
                It.Is<PatchItemRequestOptions>(options => options.FilterPredicate!.Contains("c.isDeleted = false")),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        roomsContainer.VerifyNoOtherCalls();
    }

    [Test]
    public async Task AddAsync_UpsertsListingAndCurrentDetailProjection()
    {
        var listingsContainer = new Mock<Container>();
        var roomsContainer = new Mock<Container>();
        var outboxContainer = CreateOutboxContainer();
        RoomDocument? capturedDetail = null;
        var existingDetail = new RoomDocument
        {
            Id = "room-1",
            Deposit = 500_000m,
            LandlordPhone = "0900000000"
        };

        var readResponse = new Mock<ItemResponse<RoomDocument>>();
        readResponse.SetupGet(response => response.Resource).Returns(existingDetail);
        var currentResponse = new Mock<ItemResponse<RoomDocument>>();
        currentResponse.SetupGet(response => response.Resource).Returns(new RoomDocument
        {
            Id = "room-1",
            Deposit = 500_000m,
            LandlordPhone = "0900000000"
        });
        roomsContainer
            .SetupSequence(container => container.ReadItemAsync<RoomDocument>(
                "room-1",
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(readResponse.Object)
            .ReturnsAsync(currentResponse.Object);

        listingsContainer
            .Setup(container => container.ReadItemAsync<RoomListingDocument>(
                "room-1", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Missing listing", HttpStatusCode.NotFound, 0, "activity", 0));
        listingsContainer
            .Setup(container => container.CreateItemAsync(
                It.IsAny<RoomListingDocument>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomListingDocument>>().Object);
        roomsContainer
            .Setup(container => container.ReplaceItemAsync(
                It.IsAny<RoomDocument>(),
                "room-1",
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<RoomDocument, string, PartitionKey?, ItemRequestOptions, CancellationToken>((document, _, _, _, _) => capturedDetail = document)
            .ReturnsAsync(new Mock<ItemResponse<RoomDocument>>().Object);

        var repository = new CosmosRoomListingRepository(listingsContainer.Object, roomsContainer.Object, outboxContainer.Object);
        var listing = CreateListing(ListingRoomStatus.Occupied);

        await repository.AddAsync(listing);

        listingsContainer.Verify(container => container.CreateItemAsync(
            It.IsAny<RoomListingDocument>(),
            new PartitionKey("campus-ictu"),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Multiple(() =>
        {
            Assert.That(capturedDetail, Is.Not.Null);
            Assert.That(capturedDetail!.Status, Is.EqualTo((int)ListingRoomStatus.Occupied));
            Assert.That(capturedDetail.Deposit, Is.EqualTo(500_000m));
            Assert.That(capturedDetail.LandlordPhone, Is.EqualTo("0900000000"));
        });
    }

    [Test]
    public async Task AddAsync_WhenListingWriteFailsTransiently_RetriesAndCompletesProjection()
    {
        var listingsContainer = new Mock<Container>();
        var roomsContainer = new Mock<Container>();
        var outboxContainer = CreateOutboxContainer();
        var listingWriteAttempts = 0;

        listingsContainer
            .Setup(container => container.ReadItemAsync<RoomListingDocument>(
                "room-1", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Missing listing", HttpStatusCode.NotFound, 0, "activity", 0));
        listingsContainer
            .Setup(container => container.CreateItemAsync(
                It.IsAny<RoomListingDocument>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                listingWriteAttempts++;
                if (listingWriteAttempts == 1)
                {
                    throw new CosmosException("Transient failure", HttpStatusCode.ServiceUnavailable, 0, "activity", 0);
                }

                return Task.FromResult(new Mock<ItemResponse<RoomListingDocument>>().Object);
            });

        roomsContainer
            .Setup(container => container.ReadItemAsync<RoomDocument>(
                "room-1",
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Missing detail", HttpStatusCode.NotFound, 0, "activity", 0));
        roomsContainer
            .Setup(container => container.CreateItemAsync(
                It.IsAny<RoomDocument>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomDocument>>().Object);

        var repository = new CosmosRoomListingRepository(listingsContainer.Object, roomsContainer.Object, outboxContainer.Object);

        await repository.AddAsync(CreateListing(ListingRoomStatus.Available));

        Assert.That(listingWriteAttempts, Is.EqualTo(2));
        roomsContainer.Verify(container => container.CreateItemAsync(
            It.IsAny<RoomDocument>(),
            new PartitionKey("room-1"),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task DeleteAsync_DeletesDetailAndListingProjections()
    {
        var listingsContainer = new Mock<Container>();
        var roomsContainer = new Mock<Container>();
        var outboxContainer = CreateOutboxContainer();
        var repository = new CosmosRoomListingRepository(listingsContainer.Object, roomsContainer.Object, outboxContainer.Object);

        SetupExistingProjection<RoomDocument>(roomsContainer, new RoomDocument { Id = "room-1" });
        SetupExistingProjection<RoomListingDocument>(listingsContainer, new RoomListingDocument { Id = "room-1", CampusId = "campus-ictu" });

        roomsContainer
            .Setup(container => container.ReplaceItemAsync(
                It.IsAny<RoomDocument>(), "room-1",
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomDocument>>().Object);
        listingsContainer
            .Setup(container => container.ReplaceItemAsync(
                It.IsAny<RoomListingDocument>(), "room-1",
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomListingDocument>>().Object);

        await repository.DeleteAsync("room-1", "campus-ictu");

        roomsContainer.Verify(container => container.ReplaceItemAsync(
            It.Is<RoomDocument>(document => document.IsDeleted), "room-1", new PartitionKey("room-1"), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        listingsContainer.Verify(container => container.ReplaceItemAsync(
            It.Is<RoomListingDocument>(document => document.IsDeleted), "room-1", new PartitionKey("campus-ictu"), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task AddAsync_WhenDetailWriteFailsAfterAllRetries_LeavesDurableSyncIntent()
    {
        var listingsContainer = new Mock<Container>();
        var roomsContainer = new Mock<Container>();
        var outboxContainer = CreateOutboxContainer();
        var detailWriteAttempts = 0;

        listingsContainer.Setup(container => container.ReadItemAsync<RoomListingDocument>(
                "room-1", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Missing listing", HttpStatusCode.NotFound, 0, "activity", 0));
        listingsContainer.Setup(container => container.CreateItemAsync(
                It.IsAny<RoomListingDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomListingDocument>>().Object);
        roomsContainer.Setup(container => container.ReadItemAsync<RoomDocument>(
                "room-1", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Missing detail", HttpStatusCode.NotFound, 0, "activity", 0));
        roomsContainer.Setup(container => container.CreateItemAsync(
                It.IsAny<RoomDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                detailWriteAttempts++;
                throw new CosmosException("Unavailable", HttpStatusCode.ServiceUnavailable, 0, "activity", 0);
            });

        var repository = new CosmosRoomListingRepository(listingsContainer.Object, roomsContainer.Object, outboxContainer.Object);

        Assert.ThrowsAsync<CosmosException>(() => repository.AddAsync(CreateListing(ListingRoomStatus.Occupied)));

        Assert.That(detailWriteAttempts, Is.EqualTo(3));
        outboxContainer.Verify(container => container.CreateItemAsync(
            It.Is<RoomProjectionSyncDocument>(intent => intent.Operation == "upsert" && intent.RoomId == "room-1"),
            new PartitionKey("room-1"), null, It.IsAny<CancellationToken>()), Times.Once);
        outboxContainer.Verify(container => container.ReplaceItemAsync(
            It.Is<RoomProjectionSyncDocument>(intent => intent.State == RoomProjectionSyncDocument.CompletedState), It.IsAny<string>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task AddAsync_WhenDetailReadFailsTransiently_RetriesBeforeWritingProjection()
    {
        var listingsContainer = new Mock<Container>();
        var roomsContainer = new Mock<Container>();
        var outboxContainer = CreateOutboxContainer();
        var readAttempts = 0;

        listingsContainer.Setup(container => container.ReadItemAsync<RoomListingDocument>(
                "room-1", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Missing listing", HttpStatusCode.NotFound, 0, "activity", 0));
        listingsContainer.Setup(container => container.CreateItemAsync(
                It.IsAny<RoomListingDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomListingDocument>>().Object);
        roomsContainer.Setup(container => container.ReadItemAsync<RoomDocument>(
                "room-1", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                readAttempts++;
                if (readAttempts == 1)
                {
                    throw new CosmosException("Unavailable", HttpStatusCode.ServiceUnavailable, 0, "activity", 0);
                }

                throw new CosmosException("Missing detail", HttpStatusCode.NotFound, 0, "activity", 0);
            });
        roomsContainer.Setup(container => container.CreateItemAsync(
                It.IsAny<RoomDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomDocument>>().Object);

        var repository = new CosmosRoomListingRepository(listingsContainer.Object, roomsContainer.Object, outboxContainer.Object);

        await repository.AddAsync(CreateListing(ListingRoomStatus.Available));

        Assert.That(readAttempts, Is.EqualTo(3));
    }

    [Test]
    public async Task DeleteAsync_WhenListingDeleteFailsAfterDetailDelete_LeavesDurableSyncIntent()
    {
        var listingsContainer = new Mock<Container>();
        var roomsContainer = new Mock<Container>();
        var outboxContainer = CreateOutboxContainer();
        var listingDeleteAttempts = 0;

        SetupExistingProjection<RoomDocument>(roomsContainer, new RoomDocument { Id = "room-1" });
        SetupExistingProjection<RoomListingDocument>(listingsContainer, new RoomListingDocument { Id = "room-1", CampusId = "campus-ictu" });

        roomsContainer.Setup(container => container.ReplaceItemAsync(
                It.IsAny<RoomDocument>(), "room-1", It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomDocument>>().Object);
        listingsContainer.Setup(container => container.ReplaceItemAsync(
                It.IsAny<RoomListingDocument>(), "room-1", It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                listingDeleteAttempts++;
                throw new CosmosException("Unavailable", HttpStatusCode.ServiceUnavailable, 0, "activity", 0);
            });

        var repository = new CosmosRoomListingRepository(listingsContainer.Object, roomsContainer.Object, outboxContainer.Object);

        Assert.ThrowsAsync<CosmosException>(() => repository.DeleteAsync("room-1", "campus-ictu"));

        Assert.That(listingDeleteAttempts, Is.EqualTo(3));
        outboxContainer.Verify(container => container.CreateItemAsync(
            It.Is<RoomProjectionSyncDocument>(intent => intent.Operation == "delete" && intent.RoomId == "room-1"),
            new PartitionKey("room-1"), null, It.IsAny<CancellationToken>()), Times.Once);
        outboxContainer.Verify(container => container.ReplaceItemAsync(
            It.Is<RoomProjectionSyncDocument>(intent => intent.State == RoomProjectionSyncDocument.CompletedState), It.IsAny<string>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ReconcilePendingAsync_WhenWorkerRunsBeforeOriginalListingWrite_UsesIntentSnapshotAndClearsIntent()
    {
        var listingsContainer = new Mock<Container>();
        var roomsContainer = new Mock<Container>();
        var outboxContainer = CreateOutboxContainer();
        var intent = new RoomProjectionSyncDocument
        {
            Id = "room-1",
            RoomId = "room-1",
            CampusId = "campus-ictu",
            Operation = "upsert",
            Listing = CreateListing(ListingRoomStatus.Occupied).ToDocument(),
            Sequence = 1,
            ETag = "etag-1"
        };

        var feedResponse = new Mock<FeedResponse<RoomProjectionSyncDocument>>();
        feedResponse.Setup(page => page.GetEnumerator()).Returns(new List<RoomProjectionSyncDocument> { intent }.GetEnumerator());
        var iterator = new Mock<FeedIterator<RoomProjectionSyncDocument>>();
        iterator.SetupSequence(feed => feed.HasMoreResults).Returns(true).Returns(false);
        iterator.Setup(feed => feed.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(feedResponse.Object);
        outboxContainer.Setup(container => container.GetItemQueryIterator<RoomProjectionSyncDocument>(
                It.IsAny<QueryDefinition>(), null, null))
            .Returns(iterator.Object);

        listingsContainer.Setup(container => container.ReadItemAsync<RoomListingDocument>(
                "room-1", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Missing listing", HttpStatusCode.NotFound, 0, "activity", 0));
        listingsContainer.Setup(container => container.CreateItemAsync(
                It.IsAny<RoomListingDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomListingDocument>>().Object);
        roomsContainer.Setup(container => container.ReadItemAsync<RoomDocument>(
                "room-1", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Missing detail", HttpStatusCode.NotFound, 0, "activity", 0));
        roomsContainer.Setup(container => container.CreateItemAsync(
                It.IsAny<RoomDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomDocument>>().Object);

        var repository = new CosmosRoomListingRepository(listingsContainer.Object, roomsContainer.Object, outboxContainer.Object);

        await repository.ReconcilePendingAsync();

        listingsContainer.Verify(container => container.CreateItemAsync(
            It.Is<RoomListingDocument>(listing => listing.Status == (int)ListingRoomStatus.Occupied),
            new PartitionKey("campus-ictu"), null, It.IsAny<CancellationToken>()), Times.Once);
        roomsContainer.Verify(container => container.CreateItemAsync(
            It.Is<RoomDocument>(detail => detail.Status == (int)ListingRoomStatus.Occupied),
            new PartitionKey("room-1"), null, It.IsAny<CancellationToken>()), Times.Once);
        outboxContainer.Verify(container => container.ReplaceItemAsync(
            It.Is<RoomProjectionSyncDocument>(document => document.State == RoomProjectionSyncDocument.CompletedState),
            "room-1", new PartitionKey("room-1"), It.Is<ItemRequestOptions>(options => options.IfMatchEtag == "etag-1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ReconcilePendingAsync_WhenRequestReplacedIntent_DoesNotDeleteTheNewerIntent()
    {
        var listingsContainer = new Mock<Container>();
        var roomsContainer = new Mock<Container>();
        var outboxContainer = CreateOutboxContainer();
        var intent = new RoomProjectionSyncDocument
        {
            Id = "room-1",
            RoomId = "room-1",
            CampusId = "campus-ictu",
            Operation = "delete",
            Sequence = 1,
            ETag = "stale-etag"
        };

        var feedResponse = new Mock<FeedResponse<RoomProjectionSyncDocument>>();
        feedResponse.Setup(page => page.GetEnumerator()).Returns(new List<RoomProjectionSyncDocument> { intent }.GetEnumerator());
        var iterator = new Mock<FeedIterator<RoomProjectionSyncDocument>>();
        iterator.SetupSequence(feed => feed.HasMoreResults).Returns(true).Returns(false);
        iterator.Setup(feed => feed.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(feedResponse.Object);
        outboxContainer.Setup(container => container.GetItemQueryIterator<RoomProjectionSyncDocument>(
                It.IsAny<QueryDefinition>(), null, null))
            .Returns(iterator.Object);
        outboxContainer.Setup(container => container.ReplaceItemAsync(
                It.IsAny<RoomProjectionSyncDocument>(), "room-1", It.IsAny<PartitionKey?>(), It.Is<ItemRequestOptions>(options => options.IfMatchEtag == "stale-etag"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Intent was replaced", HttpStatusCode.PreconditionFailed, 0, "activity", 0));
        SetupExistingProjection<RoomDocument>(roomsContainer, new RoomDocument { Id = "room-1" });
        SetupExistingProjection<RoomListingDocument>(listingsContainer, new RoomListingDocument { Id = "room-1", CampusId = "campus-ictu" });
        roomsContainer.Setup(container => container.DeleteItemAsync<RoomDocument>(
                "room-1", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomDocument>>().Object);
        listingsContainer.Setup(container => container.DeleteItemAsync<RoomListingDocument>(
                "room-1", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomListingDocument>>().Object);

        var repository = new CosmosRoomListingRepository(listingsContainer.Object, roomsContainer.Object, outboxContainer.Object);

        Assert.DoesNotThrowAsync(() => repository.ReconcilePendingAsync());

        outboxContainer.Verify(container => container.ReplaceItemAsync(
            It.Is<RoomProjectionSyncDocument>(document => document.State == RoomProjectionSyncDocument.CompletedState),
            "room-1", new PartitionKey("room-1"), It.Is<ItemRequestOptions>(options => options.IfMatchEtag == "stale-etag"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ReconcilePendingAsync_WhenNewerTombstoneExists_DoesNotRecreateDeletedRoom()
    {
        var listingsContainer = new Mock<Container>();
        var roomsContainer = new Mock<Container>();
        var outboxContainer = CreateOutboxContainer();
        var staleIntent = new RoomProjectionSyncDocument
        {
            Id = "room-1",
            RoomId = "room-1",
            CampusId = "campus-ictu",
            Operation = "upsert",
            Listing = CreateListing(ListingRoomStatus.Available).ToDocument(),
            Sequence = 1,
            ETag = "stale-etag"
        };
        SetupPendingIntentQuery(outboxContainer, staleIntent);
        SetupExistingProjection<RoomListingDocument>(listingsContainer, new RoomListingDocument
        {
            Id = "room-1",
            CampusId = "campus-ictu",
            Status = (int)ListingRoomStatus.Occupied,
            ProjectionVersion = 2,
            IsDeleted = true
        });

        var detailForMerge = new RoomDocument { Id = "room-1", Status = (int)ListingRoomStatus.Occupied, ProjectionVersion = 2, IsDeleted = true };
        var detailCurrent = new RoomDocument { Id = "room-1", Status = (int)ListingRoomStatus.Occupied, ProjectionVersion = 2, IsDeleted = true };
        var detailForMergeResponse = new Mock<ItemResponse<RoomDocument>>();
        detailForMergeResponse.SetupGet(response => response.Resource).Returns(detailForMerge);
        var detailCurrentResponse = new Mock<ItemResponse<RoomDocument>>();
        detailCurrentResponse.SetupGet(response => response.Resource).Returns(detailCurrent);
        roomsContainer.SetupSequence(container => container.ReadItemAsync<RoomDocument>(
                "room-1", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(detailForMergeResponse.Object)
            .ReturnsAsync(detailCurrentResponse.Object);

        var repository = new CosmosRoomListingRepository(listingsContainer.Object, roomsContainer.Object, outboxContainer.Object);

        await repository.ReconcilePendingAsync();

        listingsContainer.Verify(container => container.CreateItemAsync(
            It.IsAny<RoomListingDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        listingsContainer.Verify(container => container.ReplaceItemAsync(
            It.IsAny<RoomListingDocument>(), It.IsAny<string>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        roomsContainer.Verify(container => container.CreateItemAsync(
            It.IsAny<RoomDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        roomsContainer.Verify(container => container.ReplaceItemAsync(
            It.IsAny<RoomDocument>(), It.IsAny<string>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task AddAsync_WhenCampusChanges_TombstonesListingInPreviousCampusPartition()
    {
        var listingsContainer = new Mock<Container>();
        var roomsContainer = new Mock<Container>();
        var outboxContainer = CreateOutboxContainer();
        var priorIntent = new RoomProjectionSyncDocument
        {
            Id = "room-1",
            RoomId = "room-1",
            CampusId = "campus-A",
            Sequence = 4,
            State = RoomProjectionSyncDocument.CompletedState,
            ETag = "prior-etag"
        };
        var priorIntentResponse = new Mock<ItemResponse<RoomProjectionSyncDocument>>();
        priorIntentResponse.SetupGet(response => response.Resource).Returns(priorIntent);
        outboxContainer.Setup(container => container.ReadItemAsync<RoomProjectionSyncDocument>(
                "room-1", new PartitionKey("room-1"), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(priorIntentResponse.Object);

        listingsContainer.Setup(container => container.ReadItemAsync<RoomListingDocument>(
                "room-1", new PartitionKey("campus-B"), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Missing new listing", HttpStatusCode.NotFound, 0, "activity", 0));
        var oldListingResponse = new Mock<ItemResponse<RoomListingDocument>>();
        oldListingResponse.SetupGet(response => response.Resource).Returns(new RoomListingDocument
        {
            Id = "room-1",
            CampusId = "campus-A",
            ProjectionVersion = 4
        });
        listingsContainer.Setup(container => container.ReadItemAsync<RoomListingDocument>(
                "room-1", new PartitionKey("campus-A"), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldListingResponse.Object);
        listingsContainer.Setup(container => container.CreateItemAsync(
                It.IsAny<RoomListingDocument>(), new PartitionKey("campus-B"), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomListingDocument>>().Object);
        listingsContainer.Setup(container => container.ReplaceItemAsync(
                It.IsAny<RoomListingDocument>(), "room-1", new PartitionKey("campus-A"), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomListingDocument>>().Object);
        roomsContainer.Setup(container => container.ReadItemAsync<RoomDocument>(
                "room-1", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Missing detail", HttpStatusCode.NotFound, 0, "activity", 0));
        roomsContainer.Setup(container => container.CreateItemAsync(
                It.IsAny<RoomDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomDocument>>().Object);

        var repository = new CosmosRoomListingRepository(listingsContainer.Object, roomsContainer.Object, outboxContainer.Object);
        var movedListing = CreateListing(ListingRoomStatus.Available, "campus-B");

        await repository.AddAsync(movedListing);

        listingsContainer.Verify(container => container.ReplaceItemAsync(
            It.Is<RoomListingDocument>(document => document.IsDeleted && document.ProjectionVersion == 5),
            "room-1", new PartitionKey("campus-A"), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task AddAsync_WhenPriorIntentIsStillPendingWithCleanup_PreservesPreviousCampusInNextIntent()
    {
        var listingsContainer = new Mock<Container>();
        var roomsContainer = new Mock<Container>();
        var outboxContainer = CreateOutboxContainer();

        listingsContainer.Setup(c => c.ReadItemAsync<RoomListingDocument>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Missing", HttpStatusCode.NotFound, 0, "activity", 0));
        listingsContainer.Setup(c => c.CreateItemAsync(It.IsAny<RoomListingDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomListingDocument>>().Object);
        listingsContainer.Setup(c => c.ReplaceItemAsync(It.IsAny<RoomListingDocument>(), It.IsAny<string>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomListingDocument>>().Object);

        roomsContainer.Setup(c => c.ReadItemAsync<RoomDocument>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Missing", HttpStatusCode.NotFound, 0, "activity", 0));
        roomsContainer.Setup(c => c.CreateItemAsync(It.IsAny<RoomDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomDocument>>().Object);

        // Existing intent: moved to campus-B, but tombstone for campus-A is STILL PENDING
        var pendingIntent = new RoomProjectionSyncDocument
        {
            Id = "room-1",
            RoomId = "room-1",
            CampusId = "campus-B",
            PreviousCampusIds = ["campus-A"],
            Sequence = 2,
            State = RoomProjectionSyncDocument.PendingState,
            ETag = "pending-etag"
        };
        var pendingIntentResponse = new Mock<ItemResponse<RoomProjectionSyncDocument>>();
        pendingIntentResponse.SetupGet(response => response.Resource).Returns(pendingIntent);
        outboxContainer.Setup(container => container.ReadItemAsync<RoomProjectionSyncDocument>(
                "room-1", new PartitionKey("room-1"), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingIntentResponse.Object);

        RoomProjectionSyncDocument? capturedNextIntent = null;
        outboxContainer.Setup(container => container.ReplaceItemAsync(
                It.IsAny<RoomProjectionSyncDocument>(), "room-1", new PartitionKey("room-1"), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .Callback<RoomProjectionSyncDocument, string, PartitionKey?, ItemRequestOptions, CancellationToken>((doc, _, _, _, _) => capturedNextIntent = doc)
            .ReturnsAsync(new Mock<ItemResponse<RoomProjectionSyncDocument>>().Object);

        var repository = new CosmosRoomListingRepository(listingsContainer.Object, roomsContainer.Object, outboxContainer.Object);

        // Next update is ALSO at campus-B
        var updateAtCampusB = CreateListing(ListingRoomStatus.Available, "campus-B");
        await repository.AddAsync(updateAtCampusB);

        Assert.That(capturedNextIntent, Is.Not.Null);
        Assert.Multiple(() =>
        {
            // Intent version 3 must NOT lose campus-A from PreviousCampusIds
            Assert.That(capturedNextIntent!.PreviousCampusIds, Does.Contain("campus-A"));
            Assert.That(capturedNextIntent.PreviousCampusId, Is.EqualTo("campus-A"));
            Assert.That(capturedNextIntent.CampusId, Is.EqualTo("campus-B"));
            Assert.That(capturedNextIntent.Sequence, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task ReconcilePendingAsync_WhenMultiplePreviousCampusesPending_TombstonesAllPreviousCampuses()
    {
        var listingsContainer = new Mock<Container>();
        var roomsContainer = new Mock<Container>();
        var outboxContainer = CreateOutboxContainer();

        listingsContainer.Setup(c => c.ReadItemAsync<RoomListingDocument>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Missing", HttpStatusCode.NotFound, 0, "activity", 0));
        listingsContainer.Setup(c => c.CreateItemAsync(It.IsAny<RoomListingDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomListingDocument>>().Object);

        roomsContainer.Setup(c => c.ReadItemAsync<RoomDocument>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Missing", HttpStatusCode.NotFound, 0, "activity", 0));
        roomsContainer.Setup(c => c.CreateItemAsync(It.IsAny<RoomDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomDocument>>().Object);

        var pendingIntent = new RoomProjectionSyncDocument
        {
            Id = "room-1",
            RoomId = "room-1",
            CampusId = "campus-C",
            PreviousCampusIds = ["campus-A", "campus-B"],
            Operation = "upsert",
            Sequence = 5,
            State = RoomProjectionSyncDocument.PendingState,
            Listing = new RoomListingDocument { Id = "room-1", CampusId = "campus-C" },
            ETag = "pending-etag"
        };
        SetupPendingIntentQuery(outboxContainer, pendingIntent);

        var repository = new CosmosRoomListingRepository(listingsContainer.Object, roomsContainer.Object, outboxContainer.Object);

        await repository.ReconcilePendingAsync();

        // Must tombstone both campus-A and campus-B in listings container
        listingsContainer.Verify(container => container.CreateItemAsync(
            It.Is<RoomListingDocument>(doc => doc.Id == "room-1" && doc.CampusId == "campus-A" && doc.IsDeleted),
            new PartitionKey("campus-A"), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);

        listingsContainer.Verify(container => container.CreateItemAsync(
            It.Is<RoomListingDocument>(doc => doc.Id == "room-1" && doc.CampusId == "campus-B" && doc.IsDeleted),
            new PartitionKey("campus-B"), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<Container> CreateOutboxContainer()
    {
        var outboxContainer = new Mock<Container>();
        outboxContainer.Setup(container => container.ReadItemAsync<RoomProjectionSyncDocument>(
                It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Missing intent", HttpStatusCode.NotFound, 0, "activity", 0));
        outboxContainer.Setup(container => container.CreateItemAsync(
                It.IsAny<RoomProjectionSyncDocument>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomProjectionSyncDocument>>().Object);
        outboxContainer.Setup(container => container.ReplaceItemAsync(
                It.IsAny<RoomProjectionSyncDocument>(), It.IsAny<string>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<ItemResponse<RoomProjectionSyncDocument>>().Object);
        return outboxContainer;
    }

    private static void SetupExistingProjection<T>(Mock<Container> container, T document)
        where T : class, IProjectionVersionedDocument
    {
        var response = new Mock<ItemResponse<T>>();
        response.SetupGet(item => item.Resource).Returns(document);
        container.Setup(value => value.ReadItemAsync<T>(
                "room-1", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response.Object);
    }

    private static void SetupPendingIntentQuery(Mock<Container> outboxContainer, RoomProjectionSyncDocument intent)
    {
        var response = new Mock<FeedResponse<RoomProjectionSyncDocument>>();
        response.Setup(page => page.GetEnumerator()).Returns(new List<RoomProjectionSyncDocument> { intent }.GetEnumerator());
        var iterator = new Mock<FeedIterator<RoomProjectionSyncDocument>>();
        iterator.SetupSequence(feed => feed.HasMoreResults).Returns(true).Returns(false);
        iterator.Setup(feed => feed.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(response.Object);
        outboxContainer.Setup(container => container.GetItemQueryIterator<RoomProjectionSyncDocument>(
                It.IsAny<QueryDefinition>(), null, null))
            .Returns(iterator.Object);
    }

    private static RoomListing CreateListing(ListingRoomStatus status, string campusId = "campus-ictu") => new(
        id: "room-1",
        title: "Room 1",
        description: "Updated listing",
        monthlyRent: new Money(3_000_000m, "VND"),
        areaSquareMeters: 30,
        address: "ICTU",
        location: new GeoLocation(21.58, 105.81),
        status: status,
        verificationStatus: VerificationStatus.Verified,
        campusId: campusId);

    [Test]
    public async Task SearchByPolygonAsync_QueriesListingsContainerWithSpatialFilterAndReturnsPagedResults()
    {
        var listingsContainer = new Mock<Container>();
        var roomsContainer = new Mock<Container>();
        var outboxContainer = CreateOutboxContainer();

        var polygon = new GeoPolygon([
            new GeoLocation(21.585, 105.807),
            new GeoLocation(21.586, 105.807),
            new GeoLocation(21.586, 105.808),
            new GeoLocation(21.585, 105.808)
        ]);

        var countResponse = new Mock<FeedResponse<int>>();
        countResponse.Setup(r => r.GetEnumerator()).Returns(new List<int> { 1 }.GetEnumerator());
        countResponse.SetupGet(r => r.RequestCharge).Returns(2.5);

        var countIterator = new Mock<FeedIterator<int>>();
        countIterator.SetupSequence(it => it.HasMoreResults).Returns(true).Returns(false);
        countIterator.Setup(it => it.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(countResponse.Object);

        QueryDefinition? capturedCountQuery = null;
        listingsContainer
            .Setup(c => c.GetItemQueryIterator<int>(It.IsAny<QueryDefinition>(), null, null))
            .Callback<QueryDefinition, string, QueryRequestOptions>((q, _, _) => capturedCountQuery = q)
            .Returns(countIterator.Object);

        var listingDoc = new RoomListingDocument
        {
            Id = "room-poly-1",
            CampusId = "campus-ictu",
            Title = "Studio within Polygon",
            MonthlyRentAmount = 2500000m,
            MonthlyRentCurrency = "VND",
            AreaSquareMeters = 28.0,
            Address = "ICTU Thai Nguyen",
            Latitude = 21.5855,
            Longitude = 105.8075,
            Status = (int)ListingRoomStatus.Available,
            VerificationStatus = (int)VerificationStatus.Verified
        };

        var itemsResponse = new Mock<FeedResponse<RoomListingDocument>>();
        itemsResponse.Setup(r => r.GetEnumerator()).Returns(new List<RoomListingDocument> { listingDoc }.GetEnumerator());
        itemsResponse.SetupGet(r => r.RequestCharge).Returns(4.0);

        var itemsIterator = new Mock<FeedIterator<RoomListingDocument>>();
        itemsIterator.SetupSequence(it => it.HasMoreResults).Returns(true).Returns(false);
        itemsIterator.Setup(it => it.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(itemsResponse.Object);

        QueryDefinition? capturedItemsQuery = null;
        listingsContainer
            .Setup(c => c.GetItemQueryIterator<RoomListingDocument>(It.IsAny<QueryDefinition>(), null, null))
            .Callback<QueryDefinition, string, QueryRequestOptions>((q, _, _) => capturedItemsQuery = q)
            .Returns(itemsIterator.Object);

        var repository = new CosmosRoomListingRepository(
            listingsContainer.Object,
            roomsContainer.Object,
            outboxContainer.Object);

        var filter = new RoomSearchFilter(MinPrice: 1000000m, MaxPrice: 3000000m, SortBy: "price");
        var result = await repository.SearchByPolygonAsync(polygon, filter, 1, 10);

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Items, Has.Count.EqualTo(1));
            Assert.That(result.Items[0].Id, Is.EqualTo("room-poly-1"));
            Assert.That(capturedCountQuery, Is.Not.Null);
            Assert.That(capturedCountQuery!.QueryText, Does.Contain("ST_WITHIN(c.location, @polygon)"));
            Assert.That(capturedCountQuery.QueryText, Does.Contain("c.monthlyRentAmount >= @minPrice"));
            Assert.That(capturedItemsQuery, Is.Not.Null);
            Assert.That(capturedItemsQuery!.QueryText, Does.Contain("ST_WITHIN(c.location, @polygon)"));
            Assert.That(capturedItemsQuery.QueryText, Does.Contain("ORDER BY c.monthlyRentAmount"));
            Assert.That(repository.LastRequestCharge, Is.EqualTo(6.5));
        });
    }

    [Test]
    public async Task SearchByPolygonAsync_WithRoomTypeAndAmenities_AppliesAllFiltersInCosmosQueries()
    {
        var listingsContainer = new Mock<Container>();
        var roomsContainer = new Mock<Container>();
        var outboxContainer = CreateOutboxContainer();

        var polygon = new GeoPolygon([
            new GeoLocation(21.585, 105.807),
            new GeoLocation(21.586, 105.807),
            new GeoLocation(21.586, 105.808),
            new GeoLocation(21.585, 105.808)
        ]);

        var countResponse = new Mock<FeedResponse<int>>();
        countResponse.Setup(r => r.GetEnumerator()).Returns(new List<int> { 1 }.GetEnumerator());
        countResponse.SetupGet(r => r.RequestCharge).Returns(2.0);

        var countIterator = new Mock<FeedIterator<int>>();
        countIterator.SetupSequence(it => it.HasMoreResults).Returns(true).Returns(false);
        countIterator.Setup(it => it.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(countResponse.Object);

        QueryDefinition? capturedCountQuery = null;
        listingsContainer
            .Setup(c => c.GetItemQueryIterator<int>(It.IsAny<QueryDefinition>(), null, null))
            .Callback<QueryDefinition, string, QueryRequestOptions>((q, _, _) => capturedCountQuery = q)
            .Returns(countIterator.Object);

        var listingDoc = new RoomListingDocument
        {
            Id = "room-poly-2",
            CampusId = "campus-ictu",
            Title = "Studio with Amenities",
            MonthlyRentAmount = 2000000m,
            MonthlyRentCurrency = "VND",
            AreaSquareMeters = 25.0,
            Address = "ICTU Thai Nguyen",
            Latitude = 21.5855,
            Longitude = 105.8075,
            Status = (int)ListingRoomStatus.Available,
            VerificationStatus = (int)VerificationStatus.Verified,
            RoomType = "Studio",
            Amenities = ["Wifi", "AirConditioner"]
        };

        var itemsResponse = new Mock<FeedResponse<RoomListingDocument>>();
        itemsResponse.Setup(r => r.GetEnumerator()).Returns(new List<RoomListingDocument> { listingDoc }.GetEnumerator());
        itemsResponse.SetupGet(r => r.RequestCharge).Returns(3.0);

        var itemsIterator = new Mock<FeedIterator<RoomListingDocument>>();
        itemsIterator.SetupSequence(it => it.HasMoreResults).Returns(true).Returns(false);
        itemsIterator.Setup(it => it.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(itemsResponse.Object);

        QueryDefinition? capturedItemsQuery = null;
        listingsContainer
            .Setup(c => c.GetItemQueryIterator<RoomListingDocument>(It.IsAny<QueryDefinition>(), null, null))
            .Callback<QueryDefinition, string, QueryRequestOptions>((q, _, _) => capturedItemsQuery = q)
            .Returns(itemsIterator.Object);

        var repository = new CosmosRoomListingRepository(
            listingsContainer.Object,
            roomsContainer.Object,
            outboxContainer.Object);

        var filter = new RoomSearchFilter(
            MinPrice: 1500000m,
            MaxPrice: 2500000m,
            RoomType: "Studio",
            Amenities: ["Wifi", "AirConditioner"]);

        var result = await repository.SearchByPolygonAsync(polygon, filter, 1, 10);

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Items, Has.Count.EqualTo(1));
            Assert.That(result.Items[0].Id, Is.EqualTo("room-poly-2"));

            Assert.That(capturedCountQuery, Is.Not.Null);
            Assert.That(capturedCountQuery!.QueryText, Does.Contain("STRINGEQUALS(c.roomType, @roomType, true)"));
            Assert.That(capturedCountQuery.QueryText, Does.Contain("ARRAY_CONTAINS(c.amenities, @amenity_0)"));
            Assert.That(capturedCountQuery.QueryText, Does.Contain("ARRAY_CONTAINS(c.amenities, @amenity_1)"));

            Assert.That(capturedItemsQuery, Is.Not.Null);
            Assert.That(capturedItemsQuery!.QueryText, Does.Contain("STRINGEQUALS(c.roomType, @roomType, true)"));
            Assert.That(capturedItemsQuery.QueryText, Does.Contain("ARRAY_CONTAINS(c.amenities, @amenity_0)"));
            Assert.That(capturedItemsQuery.QueryText, Does.Contain("ARRAY_CONTAINS(c.amenities, @amenity_1)"));
        });
    }
}
