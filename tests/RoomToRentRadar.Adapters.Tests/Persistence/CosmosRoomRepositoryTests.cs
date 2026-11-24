using Microsoft.Azure.Cosmos;
using Moq;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Adapters.Tests.Persistence;

[TestFixture]
public class CosmosRoomRepositoryTests
{
    private Mock<Container> _containerMock = null!;
    private CosmosRoomRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _containerMock = new Mock<Container>();
        _repository = new CosmosRoomRepository(_containerMock.Object);
    }

    [Test]
    public async Task CreateAsync_ShouldPersistRoomThroughRoomDocumentWithGeoJsonLocation()
    {
        // Arrange
        var room = new Room
        {
            Id = "room-123",
            Title = "Phòng trọ cao cấp",
            Description = "Mô tả phòng",
            Price = 3000000m,
            Area = 30.5,
            Address = "123 Đường Lương Ngọc Quyến",
            Latitude = 21.5890,
            Longitude = 105.8100,
            Status = RoomStatus.Available,
            IsVerified = true,
            RoomType = "Khép kín"
        };

        RoomDocument? capturedDoc = null;
        PartitionKey? capturedPk = null;

        var mockResponse = new Mock<ItemResponse<RoomDocument>>();
        mockResponse.Setup(r => r.Resource).Returns(() => capturedDoc!);

        _containerMock
            .Setup(c => c.CreateItemAsync(
                It.IsAny<RoomDocument>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<RoomDocument, PartitionKey?, ItemRequestOptions, CancellationToken>((doc, pk, _, _) =>
            {
                capturedDoc = doc;
                capturedPk = pk;
            })
            .ReturnsAsync(mockResponse.Object);

        // Act
        var result = await _repository.CreateAsync(room);

        // Assert
        Assert.That(capturedDoc, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(capturedDoc!.Id, Is.EqualTo(room.Id));
            Assert.That(capturedDoc.Latitude, Is.EqualTo(21.5890));
            Assert.That(capturedDoc.Longitude, Is.EqualTo(105.8100));
            Assert.That(capturedDoc.Location, Is.Not.Null);
            Assert.That(capturedDoc.Location!.Type, Is.EqualTo("Point"));
            // GeoJSON coordinates format: [longitude, latitude]
            Assert.That(capturedDoc.Location.Coordinates, Has.Length.EqualTo(2));
            Assert.That(capturedDoc.Location.Coordinates[0], Is.EqualTo(105.8100));
            Assert.That(capturedDoc.Location.Coordinates[1], Is.EqualTo(21.5890));
            Assert.That(capturedPk, Is.EqualTo(new PartitionKey(room.Id)));

            // Result returned mapped back to Domain Entity
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo(room.Id));
            Assert.That(result.Latitude, Is.EqualTo(room.Latitude));
            Assert.That(result.Longitude, Is.EqualTo(room.Longitude));
            Assert.That(result.Title, Is.EqualTo(room.Title));
        });
    }

    [Test]
    public void RoomDocument_FromDomain_WithCoordinates_ShouldSetGeoJsonPoint()
    {
        var room = new Room
        {
            Id = "room-test",
            Title = "Test Room",
            Latitude = 21.0285,
            Longitude = 105.8542
        };

        var doc = RoomDocument.FromDomain(room);

        Assert.That(doc.Location, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(doc.Location!.Type, Is.EqualTo("Point"));
            Assert.That(doc.Location.Coordinates[0], Is.EqualTo(105.8542)); // Longitude first
            Assert.That(doc.Location.Coordinates[1], Is.EqualTo(21.0285));  // Latitude second
        });
    }

    [Test]
    public void RoomDocument_FromDomainAndToDomain_PreservesLandlordOwnerId()
    {
        var ownerId = Guid.NewGuid().ToString("D");
        var room = new Room { Id = "owned-listing", LandlordId = ownerId };

        var roundTripped = RoomDocument.FromDomain(room).ToDomain();

        Assert.That(roundTripped.LandlordId, Is.EqualTo(ownerId));
    }

    [Test]
    public async Task UpdateAsync_UsesExistingEtagAndPersistsOwnerAndNewContent()
    {
        var existing = new RoomDocument { Id = "owned-listing", ETag = "etag-current" };
        var readResponse = new Mock<ItemResponse<RoomDocument>>();
        readResponse.SetupGet(response => response.Resource).Returns(existing);
        _containerMock.Setup(container => container.ReadItemAsync<RoomDocument>(
                "owned-listing", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(readResponse.Object);

        RoomDocument? replacedDocument = null;
        ItemRequestOptions? replacedOptions = null;
        _containerMock.Setup(container => container.ReplaceItemAsync(
                It.IsAny<RoomDocument>(), "owned-listing", It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .Callback<RoomDocument, string, PartitionKey?, ItemRequestOptions, CancellationToken>((document, _, _, options, _) =>
            {
                replacedDocument = document;
                replacedOptions = options;
            })
            .ReturnsAsync(new Mock<ItemResponse<RoomDocument>>().Object);

        var room = new Room { Id = "owned-listing", LandlordId = Guid.NewGuid().ToString("D"), Title = "Updated" };
        await _repository.UpdateAsync(room);

        Assert.Multiple(() =>
        {
            Assert.That(replacedDocument!.LandlordId, Is.EqualTo(room.LandlordId));
            Assert.That(replacedDocument.Title, Is.EqualTo("Updated"));
            Assert.That(replacedOptions!.IfMatchEtag, Is.EqualTo("etag-current"));
        });
    }

    [Test]
    public async Task GetByIdAsync_WhenProjectionIsTombstoned_ReturnsNull()
    {
        var response = new Mock<ItemResponse<RoomDocument>>();
        response.SetupGet(item => item.Resource).Returns(new RoomDocument
        {
            Id = "room-deleted",
            IsDeleted = true,
            ProjectionVersion = 11
        });
        _containerMock.Setup(container => container.ReadItemAsync<RoomDocument>(
                "room-deleted", It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response.Object);

        var result = await _repository.GetByIdAsync("room-deleted");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void RoomDocument_FromDomain_WithoutCoordinates_ShouldHaveNullLocation()
    {
        var room = new Room
        {
            Id = "room-test-no-gps",
            Title = "Test Room Without GPS",
            Latitude = null,
            Longitude = null
        };

        var doc = RoomDocument.FromDomain(room);

        Assert.That(doc.Location, Is.Null);
    }

    [Test]
    public void RoomDocument_ToDomain_WhenLatLongNull_ShouldFallbackToLocationCoordinates()
    {
        var doc = new RoomDocument
        {
            Id = "room-doc-fallback",
            Title = "Test Doc",
            Latitude = null,
            Longitude = null,
            Location = new CosmosPoint
            {
                Type = "Point",
                Coordinates = [105.8542, 21.0285]
            }
        };

        var room = doc.ToDomain();

        Assert.Multiple(() =>
        {
            Assert.That(room.Longitude, Is.EqualTo(105.8542));
            Assert.That(room.Latitude, Is.EqualTo(21.0285));
        });
    }

    [Test]
    public void MergeListingFields_WhenDetailAlreadyExists_PreservesDetailOnlyFieldsAndGallery()
    {
        var existing = new RoomDocument
        {
            Id = "room-detail-1",
            Deposit = 1_500_000m,
            RoomType = "Studio",
            Bedrooms = 2,
            Bathrooms = 1,
            Amenities = ["Air conditioner", "Washing machine"],
            HouseRules = ["No smoking"],
            LandlordPhone = "0912345678",
            LastUpdatedDate = new DateTime(2026, 11, 18, 9, 30, 0, DateTimeKind.Utc),
            ImageUrls = ["https://images.example/gallery-1.jpg", "https://images.example/gallery-2.jpg"]
        };
        var listing = new RoomListingDocument
        {
            Id = existing.Id,
            Title = "Updated listing title",
            Description = "Updated listing description",
            MonthlyRentAmount = 3_000_000m,
            AreaSquareMeters = 28,
            Address = "Updated address",
            ThumbnailUrl = "https://images.example/new-thumbnail.jpg",
            VerificationStatus = 2,
            Status = (int)RoomStatus.Available,
            Latitude = 21.587,
            Longitude = 105.808,
            CreatedAtUtc = new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc)
        };

        var merged = RoomDocument.MergeListingFields(listing, existing);

        Assert.Multiple(() =>
        {
            Assert.That(merged.Title, Is.EqualTo(listing.Title));
            Assert.That(merged.Price, Is.EqualTo(listing.MonthlyRentAmount));
            Assert.That(merged.ThumbnailUrl, Is.EqualTo(listing.ThumbnailUrl));
            Assert.That(merged.Deposit, Is.EqualTo(1_500_000m));
            Assert.That(merged.RoomType, Is.EqualTo("Studio"));
            Assert.That(merged.Bedrooms, Is.EqualTo(2));
            Assert.That(merged.Bathrooms, Is.EqualTo(1));
            Assert.That(merged.Amenities, Is.EqualTo(["Air conditioner", "Washing machine"]));
            Assert.That(merged.HouseRules, Is.EqualTo(["No smoking"]));
            Assert.That(merged.LandlordPhone, Is.EqualTo("0912345678"));
            Assert.That(merged.LastUpdatedDate, Is.EqualTo(new DateTime(2026, 11, 18, 9, 30, 0, DateTimeKind.Utc)));
            Assert.That(merged.ImageUrls, Is.EqualTo(["https://images.example/gallery-1.jpg", "https://images.example/gallery-2.jpg"]));
        });
    }

    [Test]
    public void MergeListingFields_WhenDetailIsNew_InitializesLastUpdatedDateFromListingCreation()
    {
        var createdAt = new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc);
        var listing = new RoomListingDocument
        {
            Id = "new-room",
            CreatedAtUtc = createdAt
        };

        var merged = RoomDocument.MergeListingFields(listing, existingDetail: null);

        Assert.That(merged.LastUpdatedDate, Is.EqualTo(createdAt));
    }

    [Test]
    public async Task SearchAsync_WhenKeywordHasWhitespace_ShouldBindTrimmedKeywordParameter()
    {
        // Arrange
        QueryDefinition? capturedQueryDef = null;

        var feedResponseMock = new Mock<FeedResponse<int>>();
        feedResponseMock.Setup(r => r.GetEnumerator()).Returns(new List<int> { 0 }.GetEnumerator());

        var feedIteratorMock = new Mock<FeedIterator<int>>();
        feedIteratorMock.SetupSequence(i => i.HasMoreResults)
            .Returns(true)
            .Returns(false);
        feedIteratorMock.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedResponseMock.Object);

        _containerMock
            .Setup(c => c.GetItemQueryIterator<int>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
            .Callback<QueryDefinition, string, QueryRequestOptions>((qd, _, _) =>
            {
                capturedQueryDef = qd;
            })
            .Returns(feedIteratorMock.Object);

        var query = new SearchRoomsQuery(keyword: "  Phòng trọ giá rẻ  ");

        // Act
        await _repository.SearchAsync(query);

        // Assert
        Assert.That(capturedQueryDef, Is.Not.Null);
        var parameters = capturedQueryDef!.GetQueryParameters();
        Assert.That(parameters.Any(p => p.Name == "@keyword" && (string)p.Value == "Phòng trọ giá rẻ"), Is.True);
    }

    [Test]
    public async Task SearchAsync_WhenKeywordIsWhitespaceOnly_ShouldNotBindKeywordParameter()
    {
        // Arrange
        QueryDefinition? capturedQueryDef = null;

        var feedResponseMock = new Mock<FeedResponse<int>>();
        feedResponseMock.Setup(r => r.GetEnumerator()).Returns(new List<int> { 0 }.GetEnumerator());

        var feedIteratorMock = new Mock<FeedIterator<int>>();
        feedIteratorMock.SetupSequence(i => i.HasMoreResults)
            .Returns(true)
            .Returns(false);
        feedIteratorMock.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedResponseMock.Object);

        _containerMock
            .Setup(c => c.GetItemQueryIterator<int>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
            .Callback<QueryDefinition, string, QueryRequestOptions>((qd, _, _) =>
            {
                capturedQueryDef = qd;
            })
            .Returns(feedIteratorMock.Object);

        var query = new SearchRoomsQuery(keyword: "   ");

        // Act
        await _repository.SearchAsync(query);

        // Assert
        Assert.That(capturedQueryDef, Is.Not.Null);
        var parameters = capturedQueryDef!.GetQueryParameters();
        Assert.That(parameters.Any(p => p.Name == "@keyword"), Is.False);
        Assert.That(capturedQueryDef.QueryText, Does.Not.Contain("@keyword"));
    }

    [Test]
    public async Task GetSimilarRoomsAsync_ShouldIncludeIsVerifiedConditionInQuery()
    {
        QueryDefinition? capturedQueryDef = null;

        var feedResponseMock = new Mock<FeedResponse<RoomDocument>>();
        feedResponseMock.Setup(r => r.GetEnumerator()).Returns(new List<RoomDocument>().GetEnumerator());

        var feedIteratorMock = new Mock<FeedIterator<RoomDocument>>();
        feedIteratorMock.SetupSequence(i => i.HasMoreResults)
            .Returns(true)
            .Returns(false);
        feedIteratorMock.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedResponseMock.Object);

        _containerMock
            .Setup(c => c.GetItemQueryIterator<RoomDocument>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
            .Callback<QueryDefinition, string, QueryRequestOptions>((qd, _, _) =>
            {
                capturedQueryDef = qd;
            })
            .Returns(feedIteratorMock.Object);

        await _repository.GetSimilarRoomsAsync("target-room", price: 2000000m, area: 25.0);

        Assert.That(capturedQueryDef, Is.Not.Null);
        Assert.That(capturedQueryDef!.QueryText, Does.Contain("c.isVerified = true"));
    }
}
