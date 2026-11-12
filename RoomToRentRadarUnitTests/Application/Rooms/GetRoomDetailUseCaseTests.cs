using RoomToRentRadar.Application.UseCases.Rooms;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Ports.Outbound.Security;

namespace RoomToRentRadarUnitTests.Application.Rooms;

[TestFixture]
public class GetRoomDetailUseCaseTests
{
    private IRoomRepository _roomRepository = null!;
    private ICurrentUserContext _currentUserContext = null!;
    private GetRoomDetailUseCase _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _roomRepository = Substitute.For<IRoomRepository>();
        _currentUserContext = Substitute.For<ICurrentUserContext>();
        _sut = new GetRoomDetailUseCase(_roomRepository, _currentUserContext);
    }

    private static Room CreateSampleRoom(
        string id = "room-001",
        ListingStatus status = ListingStatus.Available,
        string landlordPhone = "0987654321")
    {
        return new Room(
            id: id,
            title: "Phòng trọ khép kín gần trường ICTU",
            description: "Phòng đầy đủ tiện nghi, giờ giấc tự do, an ninh tốt.",
            monthlyRent: 1800000m,
            depositAmount: 1800000m,
            areaSquareMeters: 25.5,
            roomType: "Khép kín",
            amenities: new List<string> { "Điều hòa", "Nóng lạnh", "Wifi", "Gác lửng" },
            fullAddress: "Số 123 Đường Z115, Xã Quyết Thắng, TP Thái Nguyên",
            landlordName: "Nguyễn Văn Chủ Trọ",
            landlordPhoneNumber: landlordPhone,
            photoUrls: new List<string> { "https://cdn.example.com/photo1.jpg", "https://cdn.example.com/photo2.jpg" },
            panorama360Url: "https://cdn.example.com/panorama.jpg",
            houseRules: new List<string> { "Không nuôi thú cưng", "Giữ yên lặng sau 23h" },
            status: status,
            lastUpdatedAt: new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc),
            isVerified: true,
            distanceToCampusKm: 0.8
        );
    }

    [Test]
    public void Constructor_WhenNullDependencies_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new GetRoomDetailUseCase(null!, _currentUserContext));
        Assert.Throws<ArgumentNullException>(() => new GetRoomDetailUseCase(_roomRepository, null!));
    }

    [Test]
    public async Task ExecuteAsync_WhenRoomExistsAndAvailable_ReturnsFullDetailsWithEmptySimilarRooms()
    {
        // Arrange (AC1, AC2)
        var sampleRoom = CreateSampleRoom(status: ListingStatus.Available);
        _roomRepository.GetByIdAsync("room-001", Arg.Any<CancellationToken>()).Returns(sampleRoom);
        _currentUserContext.IsAuthenticated.Returns(true);

        // Act
        var result = await _sut.ExecuteAsync("room-001");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo("room-001"));
        Assert.That(result.Title, Is.EqualTo("Phòng trọ khép kín gần trường ICTU"));
        Assert.That(result.Description, Is.EqualTo("Phòng đầy đủ tiện nghi, giờ giấc tự do, an ninh tốt."));
        Assert.That(result.MonthlyRent, Is.EqualTo(1800000m));
        Assert.That(result.DepositAmount, Is.EqualTo(1800000m));
        Assert.That(result.AreaSquareMeters, Is.EqualTo(25.5));
        Assert.That(result.RoomType, Is.EqualTo("Khép kín"));
        Assert.That(result.Amenities, Does.Contain("Điều hòa"));
        Assert.That(result.Amenities, Does.Contain("Nóng lạnh"));
        Assert.That(result.FullAddress, Is.EqualTo("Số 123 Đường Z115, Xã Quyết Thắng, TP Thái Nguyên"));
        Assert.That(result.LandlordName, Is.EqualTo("Nguyễn Văn Chủ Trọ"));
        Assert.That(result.PhotoUrls, Has.Count.EqualTo(2));
        Assert.That(result.Panorama360Url, Is.EqualTo("https://cdn.example.com/panorama.jpg"));
        Assert.That(result.HouseRules, Does.Contain("Không nuôi thú cưng"));
        Assert.That(result.Status, Is.EqualTo("Available"));
        Assert.That(result.LastUpdatedAt, Is.EqualTo(sampleRoom.LastUpdatedAt));
        Assert.That(result.IsAvailable, Is.True);
        Assert.That(result.UnavailableMessage, Is.Null);
        Assert.That(result.SimilarRooms, Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_WhenUserIsAuthenticated_ReturnsLandlordPhoneNumber()
    {
        // Arrange (AC4: Signed-in user can see phone number)
        var sampleRoom = CreateSampleRoom(landlordPhone: "0987654321");
        _roomRepository.GetByIdAsync("room-001", Arg.Any<CancellationToken>()).Returns(sampleRoom);
        _currentUserContext.IsAuthenticated.Returns(true);

        // Act
        var result = await _sut.ExecuteAsync("room-001");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.LandlordPhoneNumber, Is.EqualTo("0987654321"));
    }

    [Test]
    public async Task ExecuteAsync_WhenUserIsAnonymous_MasksOrOmitsLandlordPhoneNumber()
    {
        // Arrange (AC4: Guest / non-signed-in user CANNOT see phone number)
        var sampleRoom = CreateSampleRoom(landlordPhone: "0987654321");
        _roomRepository.GetByIdAsync("room-001", Arg.Any<CancellationToken>()).Returns(sampleRoom);
        _currentUserContext.IsAuthenticated.Returns(false);

        // Act
        var result = await _sut.ExecuteAsync("room-001");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.LandlordPhoneNumber, Is.Null);
    }

    [Test]
    public async Task ExecuteAsync_WhenRoomIsUnavailable_ReturnsStatusAndSuggestedSimilarRooms()
    {
        // Arrange (AC2, AC3: Status shows unavailable & suggests similar rooms)
        var unavailableRoom = CreateSampleRoom(id: "room-unavailable", status: ListingStatus.Unpublished);
        _roomRepository.GetByIdAsync("room-unavailable", Arg.Any<CancellationToken>()).Returns(unavailableRoom);
        _currentUserContext.IsAuthenticated.Returns(true);

        var similarRooms = new List<Room>
        {
            new(
                id: "room-sim-01",
                title: "Phòng trọ tương tự 01",
                description: "Mô tả phòng tương tự 01",
                monthlyRent: 1750000m,
                depositAmount: 1750000m,
                areaSquareMeters: 24.0,
                roomType: "Khép kín",
                fullAddress: "Đường Z115",
                landlordName: "Chủ trọ 1",
                landlordPhoneNumber: "0911111111",
                photoUrls: new List<string> { "https://cdn.example.com/sim1.jpg" },
                isVerified: true,
                distanceToCampusKm: 0.9
            ),
            new(
                id: "room-sim-02",
                title: "Phòng trọ tương tự 02",
                description: "Mô tả phòng tương tự 02",
                monthlyRent: 1900000m,
                depositAmount: 1900000m,
                areaSquareMeters: 26.0,
                roomType: "Khép kín",
                fullAddress: "Tổ 2, Tân Thịnh",
                landlordName: "Chủ trọ 2",
                landlordPhoneNumber: "0922222222",
                photoUrls: new List<string> { "https://cdn.example.com/sim2.jpg" },
                isVerified: false,
                distanceToCampusKm: 1.1
            )
        };

        _roomRepository.GetSimilarRoomsAsync(
            excludeRoomId: "room-unavailable",
            targetPrice: 1800000m,
            targetArea: 25.5,
            targetDistanceKm: 0.8,
            limit: 3,
            ct: Arg.Any<CancellationToken>())
            .Returns(similarRooms);

        // Act
        var result = await _sut.ExecuteAsync("room-unavailable");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsAvailable, Is.False);
        Assert.That(result.Status, Is.EqualTo("Unavailable"));
        Assert.That(result.UnavailableMessage, Is.Not.Null.And.Not.Empty);
        Assert.That(result.SimilarRooms, Has.Count.EqualTo(2));

        var firstSimilar = result.SimilarRooms[0];
        Assert.That(firstSimilar.Id, Is.EqualTo("room-sim-01"));
        Assert.That(firstSimilar.Title, Is.EqualTo("Phòng trọ tương tự 01"));
        Assert.That(firstSimilar.MonthlyRent, Is.EqualTo(1750000m));
        Assert.That(firstSimilar.ThumbnailUrl, Is.EqualTo("https://cdn.example.com/sim1.jpg"));
        Assert.That(firstSimilar.AddressSummary, Is.EqualTo("Đường Z115"));
    }

    [Test]
    public async Task ExecuteAsync_WhenRoomIsRented_TreatsAsUnavailableAndSuggestsSimilarRooms()
    {
        // Arrange (AC3: ListingStatus.Rented is also unavailable)
        var rentedRoom = CreateSampleRoom(id: "room-rented", status: ListingStatus.Rented);
        _roomRepository.GetByIdAsync("room-rented", Arg.Any<CancellationToken>()).Returns(rentedRoom);
        _currentUserContext.IsAuthenticated.Returns(true);

        var similarRooms = new List<Room>
        {
            new(
                id: "room-sim-01",
                title: "Phòng trọ tương tự 01",
                description: "Mô tả",
                monthlyRent: 1800000m,
                depositAmount: 1800000m,
                areaSquareMeters: 25.0,
                roomType: "Khép kín",
                fullAddress: "Đường Z115",
                landlordName: "Chủ trọ 1",
                landlordPhoneNumber: "0911111111",
                photoUrls: new List<string> { "https://cdn.example.com/sim.jpg" },
                isVerified: true,
                distanceToCampusKm: 0.5
            )
        };

        _roomRepository.GetSimilarRoomsAsync(
            excludeRoomId: "room-rented",
            targetPrice: 1800000m,
            targetArea: 25.5,
            targetDistanceKm: 0.8,
            limit: 3,
            ct: Arg.Any<CancellationToken>())
            .Returns(similarRooms);

        // Act
        var result = await _sut.ExecuteAsync("room-rented");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsAvailable, Is.False);
        Assert.That(result.Status, Is.EqualTo("Rented"));
        Assert.That(result.UnavailableMessage, Is.Not.Null.And.Not.Empty);
        Assert.That(result.SimilarRooms, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ExecuteAsync_WhenUnavailableAndNoSimilarRoomsFound_ReturnsEmptySimilarList()
    {
        // Arrange (AC3: Unavailable room but 0 similar rooms in repository)
        var unavailableRoom = CreateSampleRoom(id: "room-isolated", status: ListingStatus.Unpublished);
        _roomRepository.GetByIdAsync("room-isolated", Arg.Any<CancellationToken>()).Returns(unavailableRoom);
        _roomRepository.GetSimilarRoomsAsync(
            excludeRoomId: "room-isolated",
            targetPrice: 1800000m,
            targetArea: 25.5,
            targetDistanceKm: 0.8,
            limit: 3,
            ct: Arg.Any<CancellationToken>())
            .Returns(new List<Room>());

        // Act
        var result = await _sut.ExecuteAsync("room-isolated");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsAvailable, Is.False);
        Assert.That(result.UnavailableMessage, Is.Not.Null.And.Not.Empty);
        Assert.That(result.SimilarRooms, Is.Not.Null.And.Empty);
    }

    [Test]
    public async Task ExecuteAsync_WhenRoomNotFound_ReturnsNull()
    {
        // Arrange
        _roomRepository.GetByIdAsync("non-existing-id", Arg.Any<CancellationToken>()).Returns((Room?)null);

        // Act
        var result = await _sut.ExecuteAsync("non-existing-id");

        // Assert
        Assert.That(result, Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task ExecuteAsync_WhenRoomIdIsInvalidOrWhitespace_ReturnsNullWithoutCallingRepo(string? invalidId)
    {
        // Act
        var result = await _sut.ExecuteAsync(invalidId!);

        // Assert
        Assert.That(result, Is.Null);
        await _roomRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default!, default);
    }

    [Test]
    public async Task ExecuteAsync_PropagatesCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var sampleRoom = CreateSampleRoom();
        _roomRepository.GetByIdAsync("room-001", token).Returns(sampleRoom);

        // Act
        await _sut.ExecuteAsync("room-001", token);

        // Assert
        await _roomRepository.Received(1).GetByIdAsync("room-001", token);
    }

    [Test]
    public async Task ExecuteAsync_WhenRoomIsPendingVerification_ReturnsNullAndDoesNotExposeDetails()
    {
        // Arrange (M4: Unverified rooms must not be visible to public callers)
        var pendingRoom = CreateSampleRoom(id: "room-pending", status: ListingStatus.PendingReview);
        _roomRepository.GetByIdAsync("room-pending", Arg.Any<CancellationToken>()).Returns(pendingRoom);

        // Act
        var result = await _sut.ExecuteAsync("room-pending");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ExecuteAsync_WhenRoomIdHasSurroundingWhitespace_TrimsBeforeCallingRepo()
    {
        // Arrange (L4: UseCase trims input before delegating)
        var sampleRoom = CreateSampleRoom(id: "room-001");
        _roomRepository.GetByIdAsync("room-001", Arg.Any<CancellationToken>()).Returns(sampleRoom);

        // Act
        var result = await _sut.ExecuteAsync("  room-001  ");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo("room-001"));
        await _roomRepository.Received(1).GetByIdAsync("room-001", Arg.Any<CancellationToken>());
    }
}
