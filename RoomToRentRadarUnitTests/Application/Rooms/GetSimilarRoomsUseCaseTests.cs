namespace RoomToRentRadarUnitTests.Application.Rooms;

using NSubstitute;
using RoomToRentRadar.Application.UseCases.Rooms;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Outbound.Repositories;

[TestFixture]
public class GetSimilarRoomsUseCaseTests
{
    private static readonly DateTime BasePublishedAt = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Campus Ictu = new(
        "ICTU", "Trường Đại học Công nghệ Thông tin và Truyền thông", "ICTU", 21.5851, 105.8066, isDefault: true, sortOrder: 1);

    private IRoomRepository _roomRepository = null!;
    private ICampusRepository _campusRepository = null!;
    private IRoomSearchReadModel _readModel = null!;
    private GetSimilarRoomsUseCase _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _roomRepository = Substitute.For<IRoomRepository>();
        _campusRepository = Substitute.For<ICampusRepository>();
        _readModel = Substitute.For<IRoomSearchReadModel>();

        _campusRepository.GetDefaultCampusAsync(Arg.Any<CancellationToken>()).Returns(Ictu);
        _campusRepository.GetByIdAsync("ICTU", Arg.Any<CancellationToken>()).Returns(Ictu);
        _campusRepository.GetByIdAsync(Arg.Is<string>(id => id != "ICTU"), Arg.Any<CancellationToken>())
            .Returns((Campus?)null);

        _sut = new GetSimilarRoomsUseCase(_roomRepository, _campusRepository, _readModel);
    }

    private static Room CreateTargetRoom(
        string id = "target-room",
        long rent = 2_000_000L,
        RoomType type = RoomType.BoardingRoom,
        double lat = 21.5875,
        double lng = 105.8080)
    {
        return Room.Reconstitute(
            id: id,
            landlordId: "landlord-1",
            title: "Phòng trọ mục tiêu đang xét",
            roomType: type,
            monthlyRentVnd: rent,
            depositVnd: rent,
            areaM2: 20.0,
            addressLine: "Z115",
            ward: "Tân Thịnh",
            district: "TP. Thái Nguyên",
            latitude: lat,
            longitude: lng,
            status: ListingStatus.Available,
            createdAtUtc: BasePublishedAt,
            lastUpdatedAtUtc: BasePublishedAt,
            version: 1);
    }

    private static RoomSearchCandidate NewCandidate(
        string id,
        string title,
        RoomType type,
        long rent,
        double area,
        double lat,
        double lng)
    {
        return new RoomSearchCandidate(
            Id: id,
            Title: title,
            MonthlyRentVnd: rent,
            AreaM2: area,
            RoomType: type,
            Status: ListingStatus.Available,
            IsVerified: true,
            CoverThumbUrl: null,
            Latitude: lat,
            Longitude: lng,
            PublishedAtUtc: BasePublishedAt);
    }

    [Test]
    public void ExecuteAsync_WhenTargetRoomNotFound_ThrowsRoomNotFound()
    {
        // Arrange
        _roomRepository.GetByIdAsync("missing-room", Arg.Any<CancellationToken>()).Returns((Room?)null);

        // Act & Assert
        var ex = Assert.ThrowsAsync<EntityNotFoundException>(() => _sut.ExecuteAsync("missing-room"));
        Assert.That(ex!.ErrorCode, Is.EqualTo(ProblemCodes.RoomNotFound));
    }

    [Test]
    public void ExecuteAsync_WhenCampusNotFound_ThrowsCampusNotFound()
    {
        // Arrange
        var targetRoom = CreateTargetRoom();
        _roomRepository.GetByIdAsync("target-room", Arg.Any<CancellationToken>()).Returns(targetRoom);

        // Act & Assert
        var ex = Assert.ThrowsAsync<DomainValidationException>(() => _sut.ExecuteAsync("target-room", campusId: "INVALID_CAMPUS"));
        Assert.That(ex!.ErrorCode, Is.EqualTo(ProblemCodes.CampusNotFound));
    }

    [Test]
    public async Task ExecuteAsync_FiltersRentWithin20PercentAndDistanceWithin2Km()
    {
        // Arrange: Target room rent = 2,000,000. Rent range = [1,600,000; 2,400,000]
        var targetRoom = CreateTargetRoom("target-room", rent: 2_000_000L);
        _roomRepository.GetByIdAsync("target-room", Arg.Any<CancellationToken>()).Returns(targetRoom);

        var candidates = new[]
        {
            NewCandidate("cand-1", "Phòng hợp lệ 1", RoomType.BoardingRoom, 1_800_000L, 20.0, 21.5880, 105.8085), // ~0.1 km, 1.8M (in range)
            NewCandidate("cand-2", "Phòng hợp lệ 2", RoomType.BoardingRoom, 2_200_000L, 22.0, 21.5890, 105.8090), // ~0.2 km, 2.2M (in range)
            NewCandidate("cand-far", "Phòng xa quá 2km", RoomType.BoardingRoom, 1_900_000L, 20.0, 21.5495, 105.8435), // >4 km (out of distance)
            NewCandidate("cand-cheap", "Phòng quá rẻ", RoomType.BoardingRoom, 1_000_000L, 15.0, 21.5880, 105.8085), // 1.0M (out of rent range)
            NewCandidate("cand-expensive", "Phòng quá đắt", RoomType.BoardingRoom, 3_500_000L, 30.0, 21.5880, 105.8085) // 3.5M (out of rent range)
        };

        _readModel.QueryCandidatesAsync(Arg.Any<RoomSearchQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(candidates);

        // Act
        var result = await _sut.ExecuteAsync("target-room");

        // Assert: Only cand-1 and cand-2 qualify
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(r => r.Id), Is.EquivalentTo(new[] { "cand-1", "cand-2" }));
    }

    [Test]
    public async Task ExecuteAsync_ExcludesTargetRoomItselfFromSimilarResults()
    {
        // Arrange
        var targetRoom = CreateTargetRoom("target-room", rent: 2_000_000L);
        _roomRepository.GetByIdAsync("target-room", Arg.Any<CancellationToken>()).Returns(targetRoom);

        var candidates = new[]
        {
            NewCandidate("target-room", "Chính là phòng mục tiêu", RoomType.BoardingRoom, 2_000_000L, 20.0, 21.5875, 105.8080),
            NewCandidate("cand-1", "Phòng khác", RoomType.BoardingRoom, 1_900_000L, 20.0, 21.5880, 105.8085)
        };

        _readModel.QueryCandidatesAsync(Arg.Any<RoomSearchQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(candidates);

        // Act
        var result = await _sut.ExecuteAsync("target-room");

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo("cand-1"));
    }

    [Test]
    public async Task ExecuteAsync_ReturnsAtMost5Rooms()
    {
        // Arrange
        var targetRoom = CreateTargetRoom("target-room", rent: 2_000_000L);
        _roomRepository.GetByIdAsync("target-room", Arg.Any<CancellationToken>()).Returns(targetRoom);

        var candidates = Enumerable.Range(1, 10)
            .Select(i => NewCandidate($"cand-{i}", $"Phòng {i}", RoomType.BoardingRoom, 2_000_000L, 20.0, 21.5875 + i * 0.0005, 105.8080))
            .ToArray();

        _readModel.QueryCandidatesAsync(Arg.Any<RoomSearchQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(candidates);

        // Act
        var result = await _sut.ExecuteAsync("target-room");

        // Assert (BR-LST-10: max 5)
        Assert.That(result, Has.Count.EqualTo(5));
    }
}
