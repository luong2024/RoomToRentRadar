using NSubstitute;
using RoomToRentRadar.Application.UseCases.Rooms;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Application.Rooms;

[TestFixture]
public class GetRoomDetailUseCaseTests
{
    private IRoomRepository _roomRepository = null!;
    private ICurrentUserContextPort _currentUserContext = null!;
    private GetRoomDetailUseCase _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _roomRepository = Substitute.For<IRoomRepository>();
        _currentUserContext = Substitute.For<ICurrentUserContextPort>();
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
            roomType: "BoardingRoom",
            amenities: new List<string> { "AirConditioner", "WaterHeater", "Wifi" },
            fullAddress: "Số 123 Đường Z115, Xã Quyết Thắng, TP Thái Nguyên",
            landlordName: "Nguyễn Văn Chủ Trọ",
            landlordPhoneNumber: landlordPhone,
            photoUrls: new List<string> { "https://cdn.example.com/photo1.jpg", "https://cdn.example.com/photo2.jpg" },
            panorama360Url: "https://cdn.example.com/panorama.jpg",
            houseRules: new List<string> { "Không nuôi thú cưng", "Giữ yên lặng sau 23h" },
            status: status,
            lastUpdatedAt: new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc),
            isVerified: true,
            distanceToCampusKm: 0.8,
            roomTypeEnum: RoomType.BoardingRoom
        );
    }

    [Test]
    public async Task ExecuteAsync_WhenRoomExistsAndAvailable_ReturnsFullDetails()
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
        Assert.That(result.RoomType, Is.EqualTo(RoomType.BoardingRoom));
        Assert.That(result.Amenities, Does.Contain(Amenity.AirConditioner));
        Assert.That(result.Amenities, Does.Contain(Amenity.WaterHeater));
        Assert.That(result.FullAddress, Does.Contain("Số 123 Đường Z115"));
        Assert.That(result.LandlordName, Is.EqualTo("Nguyễn Văn Chủ Trọ"));
        Assert.That(result.Photos, Has.Count.EqualTo(3)); // 2 standard + 1 panorama
        Assert.That(result.Status, Is.EqualTo(ListingStatus.Available));
        Assert.That(result.IsAvailable, Is.True);
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
        Assert.That(result.Landlord.PhoneNumber, Is.EqualTo("0987654321"));
        Assert.That(result.Landlord.ZaloUrl, Is.EqualTo("https://zalo.me/0987654321"));
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
        Assert.That(result.Landlord.PhoneNumber, Is.Null);
        Assert.That(result.Landlord.ZaloUrl, Is.Null);
    }

    [Test]
    public async Task ExecuteAsync_WhenRoomIsRented_ReturnsDetailsWithRentedStatus()
    {
        // Arrange (AC2, AC3: Status shows rented)
        var rentedRoom = CreateSampleRoom(id: "room-rented", status: ListingStatus.Rented);
        _roomRepository.GetByIdAsync("room-rented", Arg.Any<CancellationToken>()).Returns(rentedRoom);
        _currentUserContext.IsAuthenticated.Returns(true);

        // Act
        var result = await _sut.ExecuteAsync("room-rented");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsAvailable, Is.False);
        Assert.That(result.Status, Is.EqualTo(ListingStatus.Rented));
    }

    [Test]
    public async Task ExecuteAsync_WhenRoomIsUnpublished_ReturnsDetailsWithUnpublishedStatus()
    {
        // Arrange (BR-LST-04a: Unpublished is viewable by public)
        var unpublishedRoom = CreateSampleRoom(id: "room-unpub", status: ListingStatus.Unpublished);
        _roomRepository.GetByIdAsync("room-unpub", Arg.Any<CancellationToken>()).Returns(unpublishedRoom);
        _currentUserContext.IsAuthenticated.Returns(true);

        // Act
        var result = await _sut.ExecuteAsync("room-unpub");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsAvailable, Is.False);
        Assert.That(result.Status, Is.EqualTo(ListingStatus.Unpublished));
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
        // Arrange (BR-LST-04a: PendingReview rooms return 404 to public callers)
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
        // Arrange
        var sampleRoom = CreateSampleRoom();
        _roomRepository.GetByIdAsync("room-001", Arg.Any<CancellationToken>()).Returns(sampleRoom);

        // Act
        var result = await _sut.ExecuteAsync("   room-001   ");

        // Assert
        Assert.That(result, Is.Not.Null);
        await _roomRepository.Received(1).GetByIdAsync("room-001", Arg.Any<CancellationToken>());
    }
}
