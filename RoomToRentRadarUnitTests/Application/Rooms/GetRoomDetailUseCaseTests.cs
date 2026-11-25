namespace RoomToRentRadarUnitTests.Application.Rooms;

using NSubstitute;
using RoomToRentRadar.Application.UseCases.Rooms;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

[TestFixture]
public class GetRoomDetailUseCaseTests
{
    private static readonly Campus Ictu = new(
        "ICTU", "Trường Đại học Công nghệ Thông tin và Truyền thông", "ICTU", 21.5851, 105.8066, isDefault: true, sortOrder: 1);
    private static readonly Campus Tnus = new(
        "TNUS", "Trường Đại học Khoa học", "TNUS", 21.5887, 105.8098, isDefault: false, sortOrder: 2);

    private IRoomRepository _roomRepository = null!;
    private IUserRepository _userRepository = null!;
    private ICampusRepository _campusRepository = null!;
    private ICurrentUserContextPort _currentUserContext = null!;
    private IRoomPhotoRepository _photoRepository = null!;
    private IPhotoStoragePort _photoStorage = null!;
    private GetRoomDetailUseCase _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _roomRepository = Substitute.For<IRoomRepository>();
        _userRepository = Substitute.For<IUserRepository>();
        _campusRepository = Substitute.For<ICampusRepository>();
        _currentUserContext = Substitute.For<ICurrentUserContextPort>();

        _campusRepository.GetDefaultCampusAsync(Arg.Any<CancellationToken>()).Returns(Ictu);
        _campusRepository.GetByIdAsync("ICTU", Arg.Any<CancellationToken>()).Returns(Ictu);
        _campusRepository.GetByIdAsync("TNUS", Arg.Any<CancellationToken>()).Returns(Tnus);
        _campusRepository.GetByIdAsync(Arg.Is<string>(id => id != "ICTU" && id != "TNUS"), Arg.Any<CancellationToken>())
            .Returns((Campus?)null);

        _photoRepository = Substitute.For<IRoomPhotoRepository>();
        _photoRepository.ListByRoomAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<RoomPhoto>());
        _photoStorage = Substitute.For<IPhotoStoragePort>();
        _photoStorage.GetPublicUrl(Arg.Any<string>()).Returns(call => $"https://media.test/{call.Arg<string>()}");

        _sut = new GetRoomDetailUseCase(
            _roomRepository, _userRepository, _campusRepository, _currentUserContext, _photoRepository, _photoStorage);
    }

    private static Room CreateSampleRoom(
        string id = "room-001",
        string landlordId = "landlord-001",
        ListingStatus status = ListingStatus.Available)
    {
        return Room.Reconstitute(
            id: id,
            landlordId: landlordId,
            title: "Phòng trọ khép kín gần cổng trường ICTU",
            roomType: RoomType.BoardingRoom,
            monthlyRentVnd: 1_800_000L,
            depositVnd: 1_800_000L,
            areaM2: 25.5,
            addressLine: "Số 123 Đường Z115",
            ward: "Tân Thịnh",
            district: "TP. Thái Nguyên",
            latitude: 21.5875,
            longitude: 105.8080,
            status: status,
            createdAtUtc: new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            lastUpdatedAtUtc: new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc),
            version: 1,
            description: "Phòng đầy đủ tiện nghi, giờ giấc tự do, an ninh tốt.",
            maxOccupants: 2,
            furnishing: "Basic",
            availableFrom: new DateOnly(2026, 11, 1),
            verifiedAtUtc: new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc),
            publishedAtUtc: new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc));
    }

    private static RoomPhoto Photo(string id, int sortOrder) => RoomPhoto.Reconstitute(
        id: id,
        roomId: "room-001",
        uploadedByUserId: "landlord-001",
        kind: PhotoKind.Standard,
        contentType: RoomPhoto.JpegContentType,
        sizeBytes: 1000,
        width: 1280,
        height: 853,
        originalPath: $"rooms/{id}/medium.jpg",
        mediumPath: $"rooms/{id}/medium.jpg",
        thumbPath: $"rooms/{id}/thumb.jpg",
        sortOrder: sortOrder,
        createdAtUtc: new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
        attachedAtUtc: new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));

    // ----------------------------------------------------------------- T00.10: photos

    [Test]
    public async Task ExecuteAsync_ReturnsPhotosOrderedBySortOrder_WithPublicUrls()
    {
        _roomRepository.GetByIdAsync("room-001", Arg.Any<CancellationToken>()).Returns(CreateSampleRoom());
        _photoRepository.ListByRoomAsync("room-001", Arg.Any<CancellationToken>())
            .Returns(new[] { Photo("p2", 1), Photo("p1", 0) });

        var detail = await _sut.ExecuteAsync("room-001");

        Assert.Multiple(() =>
        {
            Assert.That(detail.Photos.Select(p => p.Id), Is.EqualTo(new[] { "p1", "p2" }));
            Assert.That(detail.Photos[0].ThumbUrl, Is.EqualTo("https://media.test/rooms/p1/thumb.jpg"));
            Assert.That(detail.Photos[0].MediumUrl, Is.EqualTo("https://media.test/rooms/p1/medium.jpg"));
        });
    }

    [Test]
    public async Task ExecuteAsync_HiddenListing_NeverReadsPhotos()
    {
        _roomRepository.GetByIdAsync("room-001", Arg.Any<CancellationToken>())
            .Returns(CreateSampleRoom(status: ListingStatus.PendingReview));

        var ex = await AssertThrowsAsync<EntityNotFoundException>(() => _sut.ExecuteAsync("room-001"));
        Assert.That(ex.ErrorCode, Is.EqualTo(ProblemCodes.RoomNotFound));

        await _photoRepository.DidNotReceive().ListByRoomAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static User CreateSampleLandlord(string id = "landlord-001", string phone = "0987654321")
    {
        return User.Reconstitute(
            id: id,
            email: "landlord@example.com",
            fullName: "Nguyễn Văn Chủ Trọ",
            passwordHash: "hash-secret",
            role: UserRole.Landlord,
            authProvider: AuthProvider.Local,
            ssoSubjectId: null,
            phoneNumber: phone,
            studentCode: null,
            isVerified: true,
            failedLoginCount: 0,
            lockoutEndUtc: null,
            createdAtUtc: new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            updatedAtUtc: new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            refreshTokens: null,
            selectedCampusId: "ICTU",
            preferredLanguage: "vi",
            version: 1);
    }

    [Test]
    public async Task ExecuteAsync_WhenRoomExistsAndAvailable_ReturnsFullDetails()
    {
        // Arrange (AC1, AC2)
        var room = CreateSampleRoom(status: ListingStatus.Available);
        var landlord = CreateSampleLandlord();
        _roomRepository.GetByIdAsync("room-001", Arg.Any<CancellationToken>()).Returns(room);
        _userRepository.GetByIdAsync("landlord-001", Arg.Any<CancellationToken>()).Returns(landlord);
        _currentUserContext.IsAuthenticated.Returns(true);

        // Act
        var result = await _sut.ExecuteAsync("room-001");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo("room-001"));
        Assert.That(result.Title, Is.EqualTo("Phòng trọ khép kín gần cổng trường ICTU"));
        Assert.That(result.MonthlyRentVnd, Is.EqualTo(1_800_000L));
        Assert.That(result.Status, Is.EqualTo(ListingStatus.Available));
        Assert.That(result.IsVerified, Is.True);
        Assert.That(result.Landlord.DisplayName, Is.EqualTo("Nguyễn Văn Chủ Trọ"));
    }

    [Test]
    public async Task ExecuteAsync_WhenUserIsAuthenticated_ReturnsLandlordPhoneNumber()
    {
        // Arrange (AC4: Signed-in user can see phone number)
        var room = CreateSampleRoom();
        var landlord = CreateSampleLandlord(phone: "0987654321");
        _roomRepository.GetByIdAsync("room-001", Arg.Any<CancellationToken>()).Returns(room);
        _userRepository.GetByIdAsync("landlord-001", Arg.Any<CancellationToken>()).Returns(landlord);
        _currentUserContext.IsAuthenticated.Returns(true);

        // Act
        var result = await _sut.ExecuteAsync("room-001");

        // Assert
        Assert.That(result.Landlord.PhoneNumber, Is.EqualTo("0987654321"));
        Assert.That(result.Landlord.ZaloUrl, Is.EqualTo("https://zalo.me/0987654321"));
    }

    [Test]
    public async Task ExecuteAsync_WhenUserIsAnonymous_MasksLandlordPhoneNumber()
    {
        // Arrange (AC4: Anonymous user receives null phone number)
        var room = CreateSampleRoom();
        var landlord = CreateSampleLandlord(phone: "0987654321");
        _roomRepository.GetByIdAsync("room-001", Arg.Any<CancellationToken>()).Returns(room);
        _userRepository.GetByIdAsync("landlord-001", Arg.Any<CancellationToken>()).Returns(landlord);
        _currentUserContext.IsAuthenticated.Returns(false);

        // Act
        var result = await _sut.ExecuteAsync("room-001");

        // Assert
        Assert.That(result.Landlord.PhoneNumber, Is.Null);
        Assert.That(result.Landlord.ZaloUrl, Is.Null);
        Assert.That(result.Landlord.DisplayName, Is.EqualTo("Nguyễn Văn Chủ Trọ"));
    }

    [Test]
    public async Task ExecuteAsync_WhenRoomIsRented_ReturnsDetailsWithRentedStatus()
    {
        // Arrange (AC2 / BR-LST-04a: Rented is viewable by public)
        var room = CreateSampleRoom(status: ListingStatus.Rented);
        var landlord = CreateSampleLandlord();
        _roomRepository.GetByIdAsync("room-001", Arg.Any<CancellationToken>()).Returns(room);
        _userRepository.GetByIdAsync("landlord-001", Arg.Any<CancellationToken>()).Returns(landlord);

        // Act
        var result = await _sut.ExecuteAsync("room-001");

        // Assert
        Assert.That(result.Status, Is.EqualTo(ListingStatus.Rented));
    }

    [Test]
    public async Task ExecuteAsync_WhenRoomIsUnpublished_ReturnsDetailsWithUnpublishedStatus()
    {
        // Arrange (AC2 / BR-LST-04a: Unpublished is viewable by public)
        var room = CreateSampleRoom(status: ListingStatus.Unpublished);
        var landlord = CreateSampleLandlord();
        _roomRepository.GetByIdAsync("room-001", Arg.Any<CancellationToken>()).Returns(room);
        _userRepository.GetByIdAsync("landlord-001", Arg.Any<CancellationToken>()).Returns(landlord);

        // Act
        var result = await _sut.ExecuteAsync("room-001");

        // Assert
        Assert.That(result.Status, Is.EqualTo(ListingStatus.Unpublished));
    }

    [TestCase(ListingStatus.PendingReview)]
    [TestCase(ListingStatus.Rejected)]
    [TestCase(ListingStatus.Suspended)]
    public async Task ExecuteAsync_WhenRoomIsNonPublicAndCallerIsGuest_ThrowsRoomNotFound(ListingStatus hiddenStatus)
    {
        // Arrange (BR-LST-04a: PendingReview, Rejected, Suspended return 404 to non-owners)
        var room = CreateSampleRoom(status: hiddenStatus);
        _roomRepository.GetByIdAsync("room-001", Arg.Any<CancellationToken>()).Returns(room);
        _currentUserContext.IsAuthenticated.Returns(false);

        // Act & Assert
        var ex = await AssertThrowsAsync<EntityNotFoundException>(() => _sut.ExecuteAsync("room-001"));
        Assert.That(ex.ErrorCode, Is.EqualTo(ProblemCodes.RoomNotFound));
    }

    [TestCase(ListingStatus.PendingReview)]
    [TestCase(ListingStatus.Rejected)]
    [TestCase(ListingStatus.Suspended)]
    public async Task ExecuteAsync_WhenRoomIsNonPublicAndCallerIsOwner_ReturnsRoomDetails(ListingStatus hiddenStatus)
    {
        // Arrange (BR-LST-04a: Owner can view their own non-public listing)
        var room = CreateSampleRoom(status: hiddenStatus);
        var landlord = CreateSampleLandlord();
        _roomRepository.GetByIdAsync("room-001", Arg.Any<CancellationToken>()).Returns(room);
        _userRepository.GetByIdAsync("landlord-001", Arg.Any<CancellationToken>()).Returns(landlord);
        _currentUserContext.IsAuthenticated.Returns(true);
        _currentUserContext.UserId.Returns(room.LandlordId);
        _currentUserContext.Role.Returns("Landlord");

        // Act
        var result = await _sut.ExecuteAsync("room-001");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Status, Is.EqualTo(hiddenStatus));
    }

    [TestCase(ListingStatus.PendingReview)]
    [TestCase(ListingStatus.Rejected)]
    [TestCase(ListingStatus.Suspended)]
    public async Task ExecuteAsync_WhenRoomIsNonPublicAndCallerIsAdmin_ReturnsRoomDetails(ListingStatus hiddenStatus)
    {
        // Arrange (BR-LST-04a: Admin can view non-public listing)
        var room = CreateSampleRoom(status: hiddenStatus);
        var landlord = CreateSampleLandlord();
        _roomRepository.GetByIdAsync("room-001", Arg.Any<CancellationToken>()).Returns(room);
        _userRepository.GetByIdAsync("landlord-001", Arg.Any<CancellationToken>()).Returns(landlord);
        _currentUserContext.IsAuthenticated.Returns(true);
        _currentUserContext.UserId.Returns("admin-999");
        _currentUserContext.Role.Returns("Admin");

        // Act
        var result = await _sut.ExecuteAsync("room-001");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Status, Is.EqualTo(hiddenStatus));
    }

    [Test]
    public async Task ExecuteAsync_WhenRoomDoesNotExist_ThrowsRoomNotFound()
    {
        // Arrange
        _roomRepository.GetByIdAsync("non-existing-id", Arg.Any<CancellationToken>()).Returns((Room?)null);

        // Act & Assert
        var ex = await AssertThrowsAsync<EntityNotFoundException>(() => _sut.ExecuteAsync("non-existing-id"));
        Assert.That(ex.ErrorCode, Is.EqualTo(ProblemCodes.RoomNotFound));
    }

    [Test]
    public async Task ExecuteAsync_WhenCampusNotFound_ThrowsCampusNotFound()
    {
        // Arrange (BR-CMP-01)
        var room = CreateSampleRoom();
        _roomRepository.GetByIdAsync("room-001", Arg.Any<CancellationToken>()).Returns(room);

        // Act & Assert
        var ex = await AssertThrowsAsync<DomainValidationException>(() => _sut.ExecuteAsync("room-001", campusId: "UNKNOWN_CAMPUS"));
        Assert.That(ex.ErrorCode, Is.EqualTo(ProblemCodes.CampusNotFound));
    }

    [Test]
    public async Task ExecuteAsync_WithCampusId_CalculatesCorrectDistanceForCampus()
    {
        // Arrange
        var room = CreateSampleRoom();
        var landlord = CreateSampleLandlord();
        _roomRepository.GetByIdAsync("room-001", Arg.Any<CancellationToken>()).Returns(room);
        _userRepository.GetByIdAsync("landlord-001", Arg.Any<CancellationToken>()).Returns(landlord);

        // Act
        var resultIctu = await _sut.ExecuteAsync("room-001", campusId: "ICTU");
        var resultTnus = await _sut.ExecuteAsync("room-001", campusId: "TNUS");

        // Assert
        Assert.That(resultIctu.DistanceKm, Is.Not.EqualTo(resultTnus.DistanceKm));
    }

    [Test]
    public async Task ExecuteAsync_WhenRoomHasAmenitiesSafetyAndHouseRules_PopulatesAllChildren()
    {
        // Arrange
        var room = CreateSampleRoom();
        var rules = HouseRuleVersion.Create(
            roomId: "room-001",
            versionNumber: 1,
            guestPolicy: GuestPolicy.DaytimeOnly,
            cookingPolicy: CookingPolicy.Allowed,
            parkingAvailable: true,
            petsAllowed: false,
            smokingAllowed: false,
            createdByUserId: "landlord-001",
            utcNow: DateTime.UtcNow,
            quietHoursStart: "22:00",
            quietHoursEnd: "06:00",
            curfewTime: "23:00",
            guestPolicyNote: "Báo trước 1 tiếng",
            parkingRules: "Xe để trong sân",
            otherRules: "Giữ vệ sinh chung");

        room.SetChildren(
            amenities: new[] { Amenity.AirConditioner, Amenity.Wifi, Amenity.PrivateBathroom },
            safetyFeatures: new[]
            {
                new RoomSafetyFeatureEntry(SafetyFeature.Cctv, true),
                new RoomSafetyFeatureEntry(SafetyFeature.SecureEntrance, true),
                new RoomSafetyFeatureEntry(SafetyFeature.FireSafety, false)
            },
            currentHouseRules: rules);

        var landlord = CreateSampleLandlord();
        _roomRepository.GetByIdAsync("room-001", Arg.Any<CancellationToken>()).Returns(room);
        _userRepository.GetByIdAsync("landlord-001", Arg.Any<CancellationToken>()).Returns(landlord);

        // Act
        var result = await _sut.ExecuteAsync("room-001");

        // Assert - Amenities (AC1)
        Assert.That(result.Amenities, Has.Count.EqualTo(3));
        Assert.That(result.Amenities, Does.Contain(Amenity.AirConditioner));
        Assert.That(result.Amenities, Does.Contain(Amenity.Wifi));
        Assert.That(result.Amenities, Does.Contain(Amenity.PrivateBathroom));

        // Assert - House Rules (AC1)
        Assert.That(result.HouseRules, Is.Not.Null);
        Assert.That(result.HouseRules!.VersionNumber, Is.EqualTo(1));
        Assert.That(result.HouseRules.QuietHoursStart, Is.EqualTo("22:00"));
        Assert.That(result.HouseRules.QuietHoursEnd, Is.EqualTo("06:00"));
        Assert.That(result.HouseRules.CurfewTime, Is.EqualTo("23:00"));
        Assert.That(result.HouseRules.GuestPolicy, Is.EqualTo(GuestPolicy.DaytimeOnly));
        Assert.That(result.HouseRules.GuestPolicyNote, Is.EqualTo("Báo trước 1 tiếng"));
        Assert.That(result.HouseRules.CookingPolicy, Is.EqualTo(CookingPolicy.Allowed));
        Assert.That(result.HouseRules.ParkingAvailable, Is.True);
        Assert.That(result.HouseRules.ParkingRules, Is.EqualTo("Xe để trong sân"));
        Assert.That(result.HouseRules.PetsAllowed, Is.False);

        // Assert - Safety Score & Features (BR-LST-08: Unverified room reflects declared count)
        Assert.That(result.Safety, Is.Not.Null);
        Assert.That(result.Safety.IsVerified, Is.False);
        Assert.That(result.Safety.Score, Is.EqualTo(3)); // 3 declared features
        Assert.That(result.Safety.Features, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task ExecuteAsync_WhenRoomHasPhotos_OrdersBySortOrderAndExpandsStorageUrls()
    {
        // Arrange
        var room = CreateSampleRoom();
        var landlord = CreateSampleLandlord();
        _roomRepository.GetByIdAsync("room-001", Arg.Any<CancellationToken>()).Returns(room);
        _userRepository.GetByIdAsync("landlord-001", Arg.Any<CancellationToken>()).Returns(landlord);

        var photoRepo = Substitute.For<IRoomPhotoRepository>();
        var storage = Substitute.For<IPhotoStoragePort>();
        storage.GetPublicUrl(Arg.Any<string>()).Returns(ci => $"https://cdn.example.com/{ci.Arg<string>()}");

        var now = DateTime.UtcNow;
        var p1 = RoomPhoto.Reconstitute("p1", "room-001", "landlord-001", PhotoKind.Standard, "image/jpeg", 1024, 800, 600, "rooms/orig1.jpg", "rooms/med1.jpg", "rooms/thumb1.jpg", sortOrder: 2, createdAtUtc: now);
        var p2 = RoomPhoto.Reconstitute("p2", "room-001", "landlord-001", PhotoKind.Panorama, "image/jpeg", 2048, 1600, 800, "rooms/orig2.jpg", "rooms/med2.jpg", "rooms/thumb2.jpg", sortOrder: 1, createdAtUtc: now);

        photoRepo.ListByRoomAsync("room-001", Arg.Any<CancellationToken>()).Returns(new[] { p1, p2 });

        var sutWithPhotos = new GetRoomDetailUseCase(_roomRepository, _userRepository, _campusRepository, _currentUserContext, photoRepo, storage);

        // Act
        var result = await sutWithPhotos.ExecuteAsync("room-001");

        // Assert - Photos ordered by SortOrder (p2 sortOrder=1 before p1 sortOrder=2)
        Assert.That(result.Photos, Has.Count.EqualTo(2));
        Assert.That(result.Photos[0].Id, Is.EqualTo("p2"));
        Assert.That(result.Photos[0].SortOrder, Is.EqualTo(1));
        Assert.That(result.Photos[0].Kind, Is.EqualTo(PhotoKind.Panorama));
        Assert.That(result.Photos[0].ThumbUrl, Is.EqualTo("https://cdn.example.com/rooms/thumb2.jpg"));
        Assert.That(result.Photos[0].MediumUrl, Is.EqualTo("https://cdn.example.com/rooms/med2.jpg"));
        Assert.That(result.Photos[0].OriginalUrl, Is.EqualTo("https://cdn.example.com/rooms/orig2.jpg"));

        Assert.That(result.Photos[1].Id, Is.EqualTo("p1"));
        Assert.That(result.Photos[1].SortOrder, Is.EqualTo(2));
    }

    [Test]
    public async Task ExecuteAsync_AuthenticatedUserWithoutQueryCampus_UsesProfileSelectedCampus()
    {
        var room = CreateSampleRoom();
        var landlord = CreateSampleLandlord();
        var student = User.Reconstitute(
            id: "student-tnus",
            email: "student@ictu.edu.vn",
            fullName: "Sinh Viên TNUS",
            passwordHash: "hash",
            role: UserRole.Student,
            authProvider: AuthProvider.Local,
            ssoSubjectId: null,
            phoneNumber: null,
            studentCode: null,
            isVerified: true,
            failedLoginCount: 0,
            lockoutEndUtc: null,
            createdAtUtc: new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            updatedAtUtc: new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            refreshTokens: null,
            selectedCampusId: "TNUS",
            preferredLanguage: "vi",
            version: 1);

        _roomRepository.GetByIdAsync("room-001", Arg.Any<CancellationToken>()).Returns(room);
        _userRepository.GetByIdAsync("landlord-001", Arg.Any<CancellationToken>()).Returns(landlord);
        _userRepository.GetByIdAsync("student-tnus", Arg.Any<CancellationToken>()).Returns(student);
        _currentUserContext.IsAuthenticated.Returns(true);
        _currentUserContext.UserId.Returns("student-tnus");

        var expectedTnusDistance = RoomToRentRadar.Domain.Services.HaversineDistanceCalculator
            .CalculateDistanceKm(Tnus.Latitude, Tnus.Longitude, room.Latitude, room.Longitude);

        var result = await _sut.ExecuteAsync("room-001");

        Assert.That(result.DistanceKm, Is.EqualTo(expectedTnusDistance));
    }

    [Test]
    public void Constructor_WhenPhotoRepositoryOrStorageIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GetRoomDetailUseCase(_roomRepository, _userRepository, _campusRepository, _currentUserContext, null!, _photoStorage));

        Assert.Throws<ArgumentNullException>(() =>
            new GetRoomDetailUseCase(_roomRepository, _userRepository, _campusRepository, _currentUserContext, _photoRepository, null!));
    }

    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException ex)
        {
            return ex;
        }

        Assert.Fail($"Expected {typeof(TException).Name} to be thrown.");
        return null!;
    }
}
