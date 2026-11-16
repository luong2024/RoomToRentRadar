using System.Reflection;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadarUnitTests.Domain;

[TestFixture]
public class RoomEntityTests
{
    [Test]
    public void Room_HasNoPublicParameterlessConstructor()
    {
        var publicParameterlessCtors = typeof(Room)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(c => c.GetParameters().Length == 0)
            .ToList();

        Assert.That(publicParameterlessCtors, Is.Empty,
            "Room must be instantiated via constructors or static factories (Guardrail 1.5.3).");
    }

    [Test]
    public void Create_WithValidData_GeneratesIdAndDefaults()
    {
        var before = DateTime.UtcNow;

        var room = Room.Create(
            title: "Phòng trọ Sinh viên Z115",
            description: "Phòng sạch sẽ",
            price: 1_500_000m,
            area: 18.0,
            address: "Z115, Thái Nguyên",
            latitude: 21.5875,
            longitude: 105.8080,
            landlordId: "landlord-1");

        Assert.Multiple(() =>
        {
            Assert.That(room.Id, Is.Not.Empty.And.Length.EqualTo(32));
            Assert.That(room.IsAvailable, Is.True);
            Assert.That(room.IsVerified, Is.True);
            Assert.That(room.Status, Is.EqualTo(ListingStatus.Available));
            Assert.That(room.IsPubliclyVisible, Is.True);
            Assert.That(room.CreatedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(room.CreatedAtUtc, Is.InRange(before, DateTime.UtcNow));
            Assert.That(room.ConcurrencyToken, Is.Not.Null.And.Not.Empty);
        });
    }

    [Test]
    public void Reconstitute_StoresAllValues()
    {
        var createdAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        var room = Room.Reconstitute(
            id: "room-1",
            title: "Phòng trọ Sinh viên Z115",
            description: "Phòng sạch sẽ",
            price: 1_500_000m,
            area: 18.0,
            address: "Z115, Thái Nguyên",
            latitude: 21.5875,
            longitude: 105.8080,
            landlordId: "landlord-1",
            isAvailable: false,
            isVerified: false,
            createdAtUtc: createdAt,
            status: ListingStatus.Rented);

        Assert.Multiple(() =>
        {
            Assert.That(room.Id, Is.EqualTo("room-1"));
            Assert.That(room.Title, Is.EqualTo("Phòng trọ Sinh viên Z115"));
            Assert.That(room.MonthlyRentVnd, Is.EqualTo(1_500_000L));
            Assert.That(room.AreaM2, Is.EqualTo(18.0));
            Assert.That(room.Latitude, Is.EqualTo(21.5875));
            Assert.That(room.Longitude, Is.EqualTo(105.8080));
            Assert.That(room.Status, Is.EqualTo(ListingStatus.Rented));
            Assert.That(room.IsAvailable, Is.False);
            Assert.That(room.IsPubliclyVisible, Is.True); // Rented is publicly visible with banner per BR-LST-04a
            Assert.That(room.CreatedAtUtc, Is.EqualTo(createdAt));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_WhenIdIsNullOrWhitespace_ThrowsDomainValidationException(string? invalidId)
    {
        var ex = Assert.Throws<DomainValidationException>(() => new Room(
            id: invalidId!,
            title: "Phòng trọ sinh viên Z115",
            description: "Mô tả",
            monthlyRent: 1500000m,
            depositAmount: 500000m,
            areaSquareMeters: 20.0,
            roomType: "BoardingRoom",
            landlordId: "landlord-1"
        ));

        Assert.That(ex!.Message, Does.Contain("Room ID cannot be empty"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Ngắn")]
    public void Constructor_WhenTitleIsNullOrWhitespaceOrInvalidLength_ThrowsDomainValidationException(string? invalidTitle)
    {
        var ex = Assert.Throws<DomainValidationException>(() => new Room(
            id: "room-001",
            title: invalidTitle!,
            description: "Mô tả",
            monthlyRent: 1500000m,
            depositAmount: 500000m,
            areaSquareMeters: 20.0,
            roomType: "BoardingRoom",
            landlordId: "landlord-1"
        ));

        Assert.That(ex!.Message, Does.Contain("Room title"));
    }

    [TestCase(0)]
    [TestCase(-100000)]
    [TestCase(50000)]
    [TestCase(60000000)]
    public void Constructor_WhenMonthlyRentOutOfRange_ThrowsDomainValidationException(decimal invalidRent)
    {
        var ex = Assert.Throws<DomainValidationException>(() => new Room(
            id: "room-001",
            title: "Phòng trọ sinh viên Z115",
            description: "Mô tả",
            monthlyRent: invalidRent,
            depositAmount: 0m,
            areaSquareMeters: 20.0,
            roomType: "BoardingRoom",
            landlordId: "landlord-1"
        ));

        Assert.That(ex!.Message, Does.Contain("Monthly rent"));
    }

    [TestCase(-100)]
    [TestCase(5000000)]
    public void Constructor_WhenDepositAmountOutOfRange_ThrowsDomainValidationException(decimal invalidDeposit)
    {
        var ex = Assert.Throws<DomainValidationException>(() => new Room(
            id: "room-001",
            title: "Phòng trọ sinh viên Z115",
            description: "Mô tả",
            monthlyRent: 1500000m,
            depositAmount: invalidDeposit,
            areaSquareMeters: 20.0,
            roomType: "BoardingRoom",
            landlordId: "landlord-1"
        ));

        Assert.That(ex!.Message, Does.Contain("Deposit"));
    }

    [Test]
    public void Constructor_WhenDepositAmountZero_Allowed()
    {
        var room = new Room(
            id: "room-001",
            title: "Phòng trọ không cần cọc",
            description: "Mô tả",
            monthlyRent: 1500000m,
            depositAmount: 0m,
            areaSquareMeters: 20.0,
            roomType: "BoardingRoom",
            landlordId: "landlord-1"
        );

        Assert.That(room.DepositVnd, Is.EqualTo(0L));
    }

    [TestCase(0)]
    [TestCase(-5.5)]
    [TestCase(4.0)]
    [TestCase(205.0)]
    public void Constructor_WhenAreaSquareMetersOutOfRange_ThrowsDomainValidationException(double invalidArea)
    {
        var ex = Assert.Throws<DomainValidationException>(() => new Room(
            id: "room-001",
            title: "Phòng trọ sinh viên Z115",
            description: "Mô tả",
            monthlyRent: 1500000m,
            depositAmount: 500000m,
            areaSquareMeters: invalidArea,
            roomType: "BoardingRoom",
            landlordId: "landlord-1"
        ));

        Assert.That(ex!.Message, Does.Contain("Room area"));
    }

    [Test]
    public void Constructor_WhenAreaIsNaNOrInfinity_ThrowsDomainValidationException()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<DomainValidationException>(() => new Room(
                id: "room-001",
                title: "Phòng trọ sinh viên Z115",
                description: "Mô tả",
                monthlyRent: 1500000m,
                depositAmount: 500000m,
                areaSquareMeters: double.NaN,
                landlordId: "landlord-1"
            ));

            Assert.Throws<DomainValidationException>(() => new Room(
                id: "room-001",
                title: "Phòng trọ sinh viên Z115",
                description: "Mô tả",
                monthlyRent: 1500000m,
                depositAmount: 500000m,
                areaSquareMeters: double.PositiveInfinity,
                landlordId: "landlord-1"
            ));
        });
    }

    [Test]
    public void Constructor_WhenLatitudeOutOfRange_ThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() => new Room(
            id: "room-001",
            title: "Phòng trọ sinh viên Z115",
            description: "Mô tả",
            monthlyRent: 1500000m,
            depositAmount: 500000m,
            areaSquareMeters: 20.0,
            landlordId: "landlord-1",
            latitude: 95.0
        ));
    }

    [Test]
    public void Constructor_WhenLongitudeOutOfRange_ThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() => new Room(
            id: "room-001",
            title: "Phòng trọ sinh viên Z115",
            description: "Mô tả",
            monthlyRent: 1500000m,
            depositAmount: 500000m,
            areaSquareMeters: 20.0,
            landlordId: "landlord-1",
            longitude: -185.0
        ));
    }

    [Test]
    public void Constructor_WhenStatusNotDefinedInEnum_ThrowsDomainValidationException()
    {
        var invalidStatus = (ListingStatus)999;

        var ex = Assert.Throws<DomainValidationException>(() => new Room(
            id: "room-001",
            title: "Phòng trọ sinh viên Z115",
            description: "Mô tả",
            monthlyRent: 1500000m,
            depositAmount: 500000m,
            areaSquareMeters: 20.0,
            roomType: "BoardingRoom",
            status: invalidStatus,
            landlordId: "landlord-1"
        ));

        Assert.That(ex!.Message, Does.Contain("listing status"));
    }

    [Test]
    public void Constructor_DefensiveCopy_PreventsExternalListMutationFromAlteringEntity()
    {
        var mutableAmenities = new List<string> { "Wifi", "Nóng lạnh" };
        var mutablePhotos = new List<string> { "https://example.com/photo1.jpg" };
        var mutableRules = new List<string> { "Giữ yên lặng sau 23h" };

        var room = new Room(
            id: "room-001",
            title: "Phòng trọ test",
            description: "Mô tả",
            monthlyRent: 1500000m,
            depositAmount: 500000m,
            areaSquareMeters: 20.0,
            roomType: "BoardingRoom",
            amenities: mutableAmenities,
            photoUrls: mutablePhotos,
            houseRules: mutableRules,
            landlordId: "landlord-1"
        );

        mutableAmenities.Add("Bể bơi");
        mutablePhotos.Add("https://example.com/photo2.jpg");
        mutableRules.Add("Cho phép tiệc tùng");

        Assert.That(room.Amenities, Has.Count.EqualTo(2));
        Assert.That(room.Amenities, Does.Not.Contain("Bể bơi"));
        Assert.That(room.PhotoUrls, Has.Count.EqualTo(1));
        Assert.That(room.HouseRules, Has.Count.EqualTo(1));
    }

    [Test]
    public void PublicVisibility_Follows_BR_LST_04a()
    {
        // Available, Rented, Unpublished are publicly visible
        var availableRoom = CreateRoomWithStatus(ListingStatus.Available);
        var rentedRoom = CreateRoomWithStatus(ListingStatus.Rented);
        var unpublishedRoom = CreateRoomWithStatus(ListingStatus.Unpublished);

        // PendingReview, Rejected, Suspended are hidden from public (return 404)
        var pendingRoom = CreateRoomWithStatus(ListingStatus.PendingReview);
        var rejectedRoom = CreateRoomWithStatus(ListingStatus.Rejected);
        var suspendedRoom = CreateRoomWithStatus(ListingStatus.Suspended);

        Assert.Multiple(() =>
        {
            Assert.That(availableRoom.IsPubliclyVisible, Is.True);
            Assert.That(rentedRoom.IsPubliclyVisible, Is.True);
            Assert.That(unpublishedRoom.IsPubliclyVisible, Is.True);

            Assert.That(pendingRoom.IsPubliclyVisible, Is.False);
            Assert.That(rejectedRoom.IsPubliclyVisible, Is.False);
            Assert.That(suspendedRoom.IsPubliclyVisible, Is.False);
        });
    }

    [Test]
    public void StateMachine_Transitions_Follow_ADR_011()
    {
        var room = CreateRoomWithStatus(ListingStatus.PendingReview);

        // 1. PendingReview -> Approve -> Available
        room.Approve("admin-1");
        Assert.That(room.Status, Is.EqualTo(ListingStatus.Available));
        Assert.That(room.VerifiedByUserId, Is.EqualTo("admin-1"));
        Assert.That(room.VerifiedAtUtc, Is.Not.Null);

        // 2. Available -> MarkRented -> Rented
        room.MarkRented();
        Assert.That(room.Status, Is.EqualTo(ListingStatus.Rented));

        // 3. Rented -> MarkAvailable -> Available
        room.MarkAvailable();
        Assert.That(room.Status, Is.EqualTo(ListingStatus.Available));

        // 4. Available -> Unpublish -> Unpublished
        room.Unpublish();
        Assert.That(room.Status, Is.EqualTo(ListingStatus.Unpublished));

        // 5. Unpublished -> Republish -> Available
        room.Republish();
        Assert.That(room.Status, Is.EqualTo(ListingStatus.Available));

        // 6. Available -> Suspend -> Suspended
        room.Suspend("Reported scam");
        Assert.That(room.Status, Is.EqualTo(ListingStatus.Suspended));
        Assert.That(room.StatusReason, Is.EqualTo("Reported scam"));

        // 7. Suspended -> Resubmit -> PendingReview
        room.Resubmit();
        Assert.That(room.Status, Is.EqualTo(ListingStatus.PendingReview));

        // 8. PendingReview -> Reject -> Rejected
        room.Reject("Missing clear photos");
        Assert.That(room.Status, Is.EqualTo(ListingStatus.Rejected));
        Assert.That(room.StatusReason, Is.EqualTo("Missing clear photos"));

        // 9. Rejected -> Resubmit -> PendingReview
        room.Resubmit();
        Assert.That(room.Status, Is.EqualTo(ListingStatus.PendingReview));
    }

    [Test]
    public void StateMachine_IllegalTransitions_ThrowInvalidStateTransitionException()
    {
        var pendingRoom = CreateRoomWithStatus(ListingStatus.PendingReview);
        var availableRoom = CreateRoomWithStatus(ListingStatus.Available);

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidStateTransitionException>(() => pendingRoom.MarkRented());
            Assert.Throws<InvalidStateTransitionException>(() => pendingRoom.Republish());
            Assert.Throws<InvalidStateTransitionException>(() => availableRoom.Approve("admin-1"));
            Assert.Throws<InvalidStateTransitionException>(() => availableRoom.Resubmit());
        });
    }

    [Test]
    public void SetConcurrencyToken_WithValidToken_UpdatesToken()
    {
        var room = CreateRoomWithStatus(ListingStatus.Available);
        room.SetConcurrencyToken("new-etag-999");
        Assert.That(room.ConcurrencyToken, Is.EqualTo("new-etag-999"));
    }

    private static Room CreateRoomWithStatus(ListingStatus status)
    {
        return new Room(
            id: Guid.NewGuid().ToString("N"),
            title: "Phòng trọ test trạng thái",
            description: "Mô tả",
            monthlyRent: 1500000m,
            depositAmount: 500000m,
            areaSquareMeters: 20.0,
            roomType: "BoardingRoom",
            status: status,
            landlordId: "landlord-1"
        );
    }
}
