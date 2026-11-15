using NSubstitute;
using RoomToRentRadar.Application.UseCases.Rooms;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Rooms;

[TestFixture]
public class GetSimilarRoomsUseCaseTests
{
    private IRoomRepository _roomRepository = null!;
    private IRoomSearchReadModel _searchReadModel = null!;
    private ICampusRepository _campusRepository = null!;
    private GetSimilarRoomsUseCase _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _roomRepository = Substitute.For<IRoomRepository>();
        _searchReadModel = Substitute.For<IRoomSearchReadModel>();
        _campusRepository = Substitute.For<ICampusRepository>();

        var defaultCampus = new Campus("ICTU", "Trường Đại học Công nghệ Thông tin và Truyền thông", "ICTU", 21.5851, 105.8066, true, 1);
        _campusRepository.GetDefaultCampusAsync(Arg.Any<CancellationToken>()).Returns(defaultCampus);
        _campusRepository.GetByIdAsync("ICTU", Arg.Any<CancellationToken>()).Returns(defaultCampus);

        _sut = new GetSimilarRoomsUseCase(_roomRepository, _searchReadModel, _campusRepository);
    }

    [Test]
    public async Task ExecuteAsync_WhenTargetRoomExists_ReturnsSimilarRoomsFromReadModel()
    {
        // Arrange
        var targetRoom = Room.Create(
            title: "Phòng trọ target",
            description: "Mô tả phòng",
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

        var candidates = new List<RoomSearchCandidate>
        {
            new("sim-1", "Phòng 1", 1750000L, 24.0, RoomType.BoardingRoom, ListingStatus.Available, true, null, 21.5876, 105.8081, null),
            new("sim-2", "Phòng 2", 1900000L, 26.0, RoomType.BoardingRoom, ListingStatus.Available, true, null, 21.5877, 105.8082, null)
        };

        _searchReadModel.QueryCandidatesAsync(Arg.Any<RoomSearchQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(candidates);

        // Act
        var result = await _sut.ExecuteAsync(targetRoom.Id);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!, Has.Count.EqualTo(2));
        Assert.That(result!.Select(r => r.Id), Is.EquivalentTo(new[] { "sim-1", "sim-2" }));
    }

    [Test]
    public async Task ExecuteAsync_CardDistanceKm_RepresentsDistanceToCampusNotTargetRoom()
    {
        // Target room at (21.5875, 105.8080)
        // Candidate at (21.5876, 105.8081) - very close to target (~0.02 km)
        // Campus ICTU at (21.5851, 105.8066) - ~0.3 km away
        var targetRoom = Room.Create(
            title: "Phòng trọ target",
            description: "Mô tả phòng",
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

        var candidates = new List<RoomSearchCandidate>
        {
            new("sim-1", "Phòng 1", 1750000L, 24.0, RoomType.BoardingRoom, ListingStatus.Available, true, null, 21.5876, 105.8081, null)
        };

        _searchReadModel.QueryCandidatesAsync(Arg.Any<RoomSearchQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(candidates);

        // Act
        var result = await _sut.ExecuteAsync(targetRoom.Id);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result![0].DistanceKm, Is.GreaterThan(0.2), "distanceKm in card must be distance to campus per OpenAPI (BR-SRCH-03)");
    }

    [Test]
    public void ExecuteAsync_WhenCampusNotFound_ThrowsDomainValidationException()
    {
        // Arrange (M1: unknown campusId throws DomainValidationException with CAMPUS_NOT_FOUND)
        _campusRepository.GetByIdAsync("UNKNOWN", Arg.Any<CancellationToken>()).Returns((Campus?)null);

        // Act & Assert
        var ex = Assert.ThrowsAsync<DomainValidationException>(() => _sut.ExecuteAsync("room-1", campusId: "UNKNOWN"));
        Assert.That(ex!.ErrorCode, Is.EqualTo(ProblemCodes.CampusNotFound));
    }

    [Test]
    public async Task ExecuteAsync_WhenTargetRoomNotFound_ReturnsNull()
    {
        // Arrange
        _roomRepository.GetByIdAsync("not-found", Arg.Any<CancellationToken>()).Returns((Room?)null);

        // Act
        var result = await _sut.ExecuteAsync("not-found");

        // Assert
        Assert.That(result, Is.Null);
        await _searchReadModel.DidNotReceiveWithAnyArgs().QueryCandidatesAsync(default!, default);
    }

    [Test]
    public async Task ExecuteAsync_WhenTargetRoomPendingReview_ReturnsNull()
    {
        // Arrange (BR-LST-04a: PendingReview must not disclose similar rooms)
        var pendingRoom = Room.Create(
            landlordId: "landlord-1",
            title: "Phòng chờ duyệt",
            roomType: RoomType.BoardingRoom,
            monthlyRentVnd: 1800000L,
            depositVnd: 0L,
            areaM2: 25.0,
            addressLine: "Z115",
            ward: "Tân Thịnh",
            district: "TP. Thái Nguyên",
            latitude: 21.5875,
            longitude: 105.8080,
            description: "Chưa duyệt"
        );

        _roomRepository.GetByIdAsync(pendingRoom.Id, Arg.Any<CancellationToken>()).Returns(pendingRoom);

        // Act
        var result = await _sut.ExecuteAsync(pendingRoom.Id);

        // Assert
        Assert.That(result, Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task ExecuteAsync_WhenRoomIdInvalid_ReturnsNull(string? invalidId)
    {
        var result = await _sut.ExecuteAsync(invalidId!);
        Assert.That(result, Is.Null);
    }
}
