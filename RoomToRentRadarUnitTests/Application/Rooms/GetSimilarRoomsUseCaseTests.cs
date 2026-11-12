using NSubstitute;
using RoomToRentRadar.Application.UseCases.Rooms;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Rooms;

[TestFixture]
public class GetSimilarRoomsUseCaseTests
{
    private IRoomRepository _roomRepository = null!;
    private IRoomSearchReadModel _searchReadModel = null!;
    private GetSimilarRoomsUseCase _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _roomRepository = Substitute.For<IRoomRepository>();
        _searchReadModel = Substitute.For<IRoomSearchReadModel>();
        _sut = new GetSimilarRoomsUseCase(_roomRepository, _searchReadModel);
    }

    [Test]
    public async Task ExecuteAsync_WhenTargetRoomExists_ReturnsSimilarRoomsFromReadModel()
    {
        // Arrange
        var targetRoom = Room.Create(
            title: "Phòng trọ target",
            description: "Mô tả",
            price: 1800000m,
            area: 25.0,
            address: "Z115",
            latitude: 21.5875,
            longitude: 105.8080,
            landlordId: "landlord-1",
            roomType: RoomType.BoardingRoom,
            initialStatus: ListingStatus.Available
        );

        _roomRepository.GetByIdAsync(targetRoom.Id, Arg.Any<CancellationToken>()).Returns(targetRoom);

        var expectedCards = new List<RoomCardDto>
        {
            new("sim-1", "Phòng 1", 1750000m, 24.0, RoomType.BoardingRoom, 0.3, true, ListingStatus.Available, 21.58, 105.80),
            new("sim-2", "Phòng 2", 1900000m, 26.0, RoomType.BoardingRoom, 0.5, true, ListingStatus.Available, 21.59, 105.81)
        };

        _searchReadModel.GetSimilarRoomsAsync(
            targetRoom.Id,
            targetRoom.MonthlyRentVnd,
            targetRoom.Latitude,
            targetRoom.Longitude,
            targetRoom.RoomType,
            5,
            Arg.Any<CancellationToken>())
            .Returns(expectedCards);

        // Act
        var result = await _sut.ExecuteAsync(targetRoom.Id);

        // Assert
        Assert.That(result, Is.EqualTo(expectedCards));
    }

    [Test]
    public async Task ExecuteAsync_WhenTargetRoomNotFound_ReturnsEmptyList()
    {
        // Arrange
        _roomRepository.GetByIdAsync("not-found", Arg.Any<CancellationToken>()).Returns((Room?)null);

        // Act
        var result = await _sut.ExecuteAsync("not-found");

        // Assert
        Assert.That(result, Is.Empty);
        await _searchReadModel.DidNotReceiveWithAnyArgs().GetSimilarRoomsAsync(default!, default, default, default, default, default, default);
    }

    [Test]
    public async Task ExecuteAsync_WhenTargetRoomPendingReview_ReturnsEmptyList()
    {
        // Arrange (BR-LST-04a: PendingReview must not disclose similar rooms)
        var pendingRoom = Room.Create(
            title: "Phòng chờ duyệt",
            description: "Mô tả",
            price: 1800000m,
            area: 25.0,
            address: "Z115",
            latitude: 21.5875,
            longitude: 105.8080,
            landlordId: "landlord-1",
            initialStatus: ListingStatus.PendingReview
        );

        _roomRepository.GetByIdAsync(pendingRoom.Id, Arg.Any<CancellationToken>()).Returns(pendingRoom);

        // Act
        var result = await _sut.ExecuteAsync(pendingRoom.Id);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task ExecuteAsync_WhenRoomIdInvalid_ReturnsEmptyList(string? invalidId)
    {
        var result = await _sut.ExecuteAsync(invalidId!);
        Assert.That(result, Is.Empty);
    }
}
