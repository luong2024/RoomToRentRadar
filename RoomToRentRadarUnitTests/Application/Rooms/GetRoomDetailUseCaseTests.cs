using RoomToRentRadar.Application.UseCases.Rooms;
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
        RoomStatus status = RoomStatus.Available,
        string landlordPhone = "0987654321")
    {
        return new Room
        {
            Id = id,
            Title = "Phòng trọ khép kín gần trường ICTU",
            Description = "Phòng đầy đủ tiện nghi, giờ giấc tự do, an ninh tốt.",
            MonthlyRent = 1800000m,
            DepositAmount = 1800000m,
            AreaSquareMeters = 25.5,
            RoomType = "Khép kín",
            Amenities = new List<string> { "Điều hòa", "Nóng lạnh", "Wifi", "Gác lửng" },
            FullAddress = "Số 123 Đường Z115, Xã Quyết Thắng, TP Thái Nguyên",
            LandlordName = "Nguyễn Văn Chủ Trọ",
            LandlordPhoneNumber = landlordPhone,
            PhotoUrls = new List<string> { "https://cdn.example.com/photo1.jpg", "https://cdn.example.com/photo2.jpg" },
            Panorama360Url = "https://cdn.example.com/panorama.jpg",
            HouseRules = new List<string> { "Không nuôi thú cưng", "Giữ yên lặng sau 23h" },
            Status = status,
            LastUpdatedAt = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc),
            IsVerified = true,
            DistanceToCampusKm = 0.8
        };
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
        var sampleRoom = CreateSampleRoom(status: RoomStatus.Available);
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
        var unavailableRoom = CreateSampleRoom(id: "room-unavailable", status: RoomStatus.Unavailable);
        _roomRepository.GetByIdAsync("room-unavailable", Arg.Any<CancellationToken>()).Returns(unavailableRoom);
        _currentUserContext.IsAuthenticated.Returns(true);

        var similarRooms = new List<Room>
        {
            new()
            {
                Id = "room-sim-01",
                Title = "Phòng trọ tương tự 01",
                MonthlyRent = 1750000m,
                AreaSquareMeters = 24.0,
                DistanceToCampusKm = 0.9,
                IsVerified = true,
                PhotoUrls = new List<string> { "https://cdn.example.com/sim1.jpg" },
                FullAddress = "Đường Z115"
            },
            new()
            {
                Id = "room-sim-02",
                Title = "Phòng trọ tương tự 02",
                MonthlyRent = 1900000m,
                AreaSquareMeters = 26.0,
                DistanceToCampusKm = 1.1,
                IsVerified = false,
                PhotoUrls = new List<string> { "https://cdn.example.com/sim2.jpg" },
                FullAddress = "Tổ 2, Tân Thịnh"
            }
        };

        _roomRepository.GetSimilarRoomsAsync(
            excludeRoomId: "room-unavailable",
            targetPrice: 1800000m,
            targetArea: 25.5,
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
        // Arrange (AC3: RoomStatus.Rented is also unavailable)
        var rentedRoom = CreateSampleRoom(id: "room-rented", status: RoomStatus.Rented);
        _roomRepository.GetByIdAsync("room-rented", Arg.Any<CancellationToken>()).Returns(rentedRoom);
        _currentUserContext.IsAuthenticated.Returns(true);

        var similarRooms = new List<Room>
        {
            new()
            {
                Id = "room-sim-01",
                Title = "Phòng trọ tương tự 01",
                MonthlyRent = 1800000m,
                AreaSquareMeters = 25.0,
                DistanceToCampusKm = 0.5,
                IsVerified = true,
                PhotoUrls = new List<string> { "https://cdn.example.com/sim.jpg" },
                FullAddress = "Đường Z115"
            }
        };

        _roomRepository.GetSimilarRoomsAsync("room-rented", 1800000m, 25.5, 3, Arg.Any<CancellationToken>())
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
        var unavailableRoom = CreateSampleRoom(id: "room-isolated", status: RoomStatus.Unavailable);
        _roomRepository.GetByIdAsync("room-isolated", Arg.Any<CancellationToken>()).Returns(unavailableRoom);
        _roomRepository.GetSimilarRoomsAsync("room-isolated", 1800000m, 25.5, 3, Arg.Any<CancellationToken>())
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
}
