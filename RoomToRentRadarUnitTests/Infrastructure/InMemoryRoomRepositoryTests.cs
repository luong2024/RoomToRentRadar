#pragma warning disable CS0618
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Infrastructure.Adapters.Mocks;

namespace RoomToRentRadarUnitTests.Infrastructure;

[TestFixture]
public class InMemoryRoomRepositoryTests
{
    private InMemoryRoomRepository _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new InMemoryRoomRepository();
    }

    [Test]
    public async Task GetByIdAsync_WhenIdExists_ReturnsMatchingRoom()
    {
        var room = await _sut.GetByIdAsync("room-ictu-001");

        Assert.That(room, Is.Not.Null);
        Assert.That(room!.Id, Is.EqualTo("room-ictu-001"));
        Assert.That(room.Title, Does.Contain("cổng chính ICTU"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("non-existent-id")]
    public async Task GetByIdAsync_WhenIdInvalidOrNotFound_ReturnsNull(string? id)
    {
        var room = await _sut.GetByIdAsync(id!);
        Assert.That(room, Is.Null);
    }

    [Test]
    public async Task GetSimilarRoomsAsync_ExcludesSelf_AndOnlyReturnsAvailableRooms()
    {
        // Arrange: Add a rented room and an unavailable room
        var targetRoom = await _sut.GetByIdAsync("room-ictu-001");
        Assert.That(targetRoom, Is.Not.Null);

        var rentedRoom = new Room(
            id: "room-rented-test",
            title: "Phòng đã thuê",
            description: "Mô tả",
            monthlyRent: targetRoom!.MonthlyRent,
            depositAmount: 1000000m,
            areaSquareMeters: targetRoom.AreaSquareMeters,
            roomType: "Khép kín",
            status: ListingStatus.Rented,
            distanceToCampusKm: targetRoom.DistanceToCampusKm
        );
        _sut.AddOrUpdate(rentedRoom);

        // Act
        var results = await _sut.GetSimilarRoomsAsync(
            excludeRoomId: "room-ictu-001",
            targetPrice: targetRoom.MonthlyRent,
            targetArea: targetRoom.AreaSquareMeters,
            targetDistanceKm: targetRoom.DistanceToCampusKm,
            limit: 10
        );

        // Assert
        Assert.That(results, Is.Not.Empty);
        Assert.That(results.Any(r => r.Id == "room-ictu-001"), Is.False, "Must exclude the target room itself.");
        Assert.That(results.Any(r => r.Id == "room-rented-test"), Is.False, "Must exclude rented/unavailable rooms.");
        Assert.That(results.All(r => r.IsAvailable), Is.True, "All returned similar rooms must be Available.");
    }

    [Test]
    public async Task GetSimilarRoomsAsync_PrioritizesNearestDistanceFirst()
    {
        // Arrange
        var roomNear = new Room(
            id: "room-near",
            title: "Phòng gần trường",
            description: "0.3km",
            monthlyRent: 2000000m,
            depositAmount: 1000000m,
            areaSquareMeters: 25.0,
            roomType: "Khép kín",
            status: ListingStatus.Available,
            distanceToCampusKm: 0.3
        );
        var roomFar = new Room(
            id: "room-far",
            title: "Phòng xa trường",
            description: "2.5km",
            monthlyRent: 1800000m, // Price is closer to target 1800000 than roomNear, but distance is far
            depositAmount: 1000000m,
            areaSquareMeters: 25.0,
            roomType: "Khép kín",
            status: ListingStatus.Available,
            distanceToCampusKm: 2.5
        );
        _sut.AddOrUpdate(roomNear);
        _sut.AddOrUpdate(roomFar);

        // Act - target distance 0.2km
        var results = await _sut.GetSimilarRoomsAsync(
            excludeRoomId: "some-id",
            targetPrice: 1800000m,
            targetArea: 25.0,
            targetDistanceKm: 0.2,
            limit: 2
        );

        // Assert: roomNear (|0.3 - 0.2| = 0.1) must precede roomFar (|2.5 - 0.2| = 2.3)
        var nearIndex = results.ToList().FindIndex(r => r.Id == "room-near");
        var farIndex = results.ToList().FindIndex(r => r.Id == "room-far");

        Assert.That(nearIndex, Is.GreaterThanOrEqualTo(0));
        if (farIndex >= 0)
        {
            Assert.That(nearIndex, Is.LessThan(farIndex), "Rooms closer in distance to campus must be ranked higher.");
        }
    }

    [Test]
    public async Task GetSimilarRoomsAsync_RespectsSpecifiedLimit()
    {
        var results = await _sut.GetSimilarRoomsAsync(
            excludeRoomId: "room-ictu-001",
            targetPrice: 1800000m,
            targetArea: 25.0,
            targetDistanceKm: 0.2,
            limit: 1
        );

        Assert.That(results, Has.Count.EqualTo(1));
    }
}
