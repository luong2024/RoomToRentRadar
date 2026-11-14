using Microsoft.Azure.Cosmos;
using Moq;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
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
}
