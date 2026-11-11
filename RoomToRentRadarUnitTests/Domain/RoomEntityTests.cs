using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadarUnitTests.Domain;

[TestFixture]
public class RoomEntityTests
{
    [Test]
    public void Constructor_WithValidParameters_InitializesPropertiesCorrectly()
    {
        // Arrange
        var amenities = new List<string> { "Wifi", "Điều hòa" };
        var photoUrls = new List<string> { "https://example.com/photo1.jpg" };
        var houseRules = new List<string> { "Không hút thuốc" };
        var lastUpdated = new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);

        // Act
        var room = new Room(
            id: "room-001",
            title: "Phòng trọ cao cấp ICTU",
            description: "Đầy đủ tiện nghi",
            monthlyRent: 2000000m,
            depositAmount: 1000000m,
            areaSquareMeters: 25.0,
            roomType: "Khép kín",
            amenities: amenities,
            fullAddress: "Số 12 Đường Z115",
            landlordName: "Cô Lan",
            landlordPhoneNumber: "0988123456",
            photoUrls: photoUrls,
            panorama360Url: "https://my.matterport.com/show/?m=sample",
            houseRules: houseRules,
            status: RoomStatus.Available,
            lastUpdatedAt: lastUpdated,
            isVerified: true,
            distanceToCampusKm: 0.3
        );

        // Assert
        Assert.That(room.Id, Is.EqualTo("room-001"));
        Assert.That(room.Title, Is.EqualTo("Phòng trọ cao cấp ICTU"));
        Assert.That(room.Description, Is.EqualTo("Đầy đủ tiện nghi"));
        Assert.That(room.MonthlyRent, Is.EqualTo(2000000m));
        Assert.That(room.DepositAmount, Is.EqualTo(1000000m));
        Assert.That(room.AreaSquareMeters, Is.EqualTo(25.0));
        Assert.That(room.RoomType, Is.EqualTo("Khép kín"));
        Assert.That(room.Amenities, Is.EquivalentTo(amenities));
        Assert.That(room.FullAddress, Is.EqualTo("Số 12 Đường Z115"));
        Assert.That(room.LandlordName, Is.EqualTo("Cô Lan"));
        Assert.That(room.LandlordPhoneNumber, Is.EqualTo("0988123456"));
        Assert.That(room.PhotoUrls, Is.EquivalentTo(photoUrls));
        Assert.That(room.Panorama360Url, Is.EqualTo("https://my.matterport.com/show/?m=sample"));
        Assert.That(room.HouseRules, Is.EquivalentTo(houseRules));
        Assert.That(room.Status, Is.EqualTo(RoomStatus.Available));
        Assert.That(room.IsAvailable, Is.True);
        Assert.That(room.IsPendingVerification, Is.False);
        Assert.That(room.IsPubliclyVisible, Is.True);
        Assert.That(room.LastUpdatedAt, Is.EqualTo(lastUpdated));
        Assert.That(room.IsVerified, Is.True);
        Assert.That(room.DistanceToCampusKm, Is.EqualTo(0.3));
        Assert.That(room.ConcurrencyToken, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Constructor_WithCustomConcurrencyToken_PreservesProvidedToken()
    {
        // Act
        var room = new Room(
            id: "room-002",
            title: "Phòng trọ sinh viên",
            description: "Mô tả",
            monthlyRent: 1500000m,
            depositAmount: 500000m,
            areaSquareMeters: 20.0,
            roomType: "Khép kín",
            concurrencyToken: "custom-token-123"
        );

        // Assert
        Assert.That(room.ConcurrencyToken, Is.EqualTo("custom-token-123"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_WhenIdIsNullOrWhitespace_ThrowsDomainValidationException(string? invalidId)
    {
        var ex = Assert.Throws<DomainValidationException>(() => new Room(
            id: invalidId!,
            title: "Phòng trọ",
            description: "Mô tả",
            monthlyRent: 1500000m,
            depositAmount: 500000m,
            areaSquareMeters: 20.0,
            roomType: "Khép kín"
        ));

        Assert.That(ex!.Message, Does.Contain("Room ID cannot be empty"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_WhenTitleIsNullOrWhitespace_ThrowsDomainValidationException(string? invalidTitle)
    {
        var ex = Assert.Throws<DomainValidationException>(() => new Room(
            id: "room-001",
            title: invalidTitle!,
            description: "Mô tả",
            monthlyRent: 1500000m,
            depositAmount: 500000m,
            areaSquareMeters: 20.0,
            roomType: "Khép kín"
        ));

        Assert.That(ex!.Message, Does.Contain("Room title cannot be empty"));
    }

    [TestCase(0)]
    [TestCase(-100000)]
    public void Constructor_WhenMonthlyRentZeroOrNegative_ThrowsDomainValidationException(decimal invalidRent)
    {
        var ex = Assert.Throws<DomainValidationException>(() => new Room(
            id: "room-001",
            title: "Phòng trọ",
            description: "Mô tả",
            monthlyRent: invalidRent,
            depositAmount: 500000m,
            areaSquareMeters: 20.0,
            roomType: "Khép kín"
        ));

        Assert.That(ex!.Message, Does.Contain("Monthly rent must be greater than zero"));
    }

    [Test]
    public void Constructor_WhenDepositAmountNegative_ThrowsDomainValidationException()
    {
        var ex = Assert.Throws<DomainValidationException>(() => new Room(
            id: "room-001",
            title: "Phòng trọ",
            description: "Mô tả",
            monthlyRent: 1500000m,
            depositAmount: -100m,
            areaSquareMeters: 20.0,
            roomType: "Khép kín"
        ));

        Assert.That(ex!.Message, Does.Contain("Deposit amount cannot be negative"));
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
            roomType: "Khép kín"
        );

        Assert.That(room.DepositAmount, Is.EqualTo(0m));
    }

    [TestCase(0)]
    [TestCase(-5.5)]
    public void Constructor_WhenAreaSquareMetersZeroOrNegative_ThrowsDomainValidationException(double invalidArea)
    {
        var ex = Assert.Throws<DomainValidationException>(() => new Room(
            id: "room-001",
            title: "Phòng trọ",
            description: "Mô tả",
            monthlyRent: 1500000m,
            depositAmount: 500000m,
            areaSquareMeters: invalidArea,
            roomType: "Khép kín"
        ));

        Assert.That(ex!.Message, Does.Contain("Area square meters must be greater than zero"));
    }

    [Test]
    public void Constructor_WhenStatusNotDefinedInEnum_ThrowsDomainValidationException()
    {
        var invalidStatus = (RoomStatus)999;

        var ex = Assert.Throws<DomainValidationException>(() => new Room(
            id: "room-001",
            title: "Phòng trọ",
            description: "Mô tả",
            monthlyRent: 1500000m,
            depositAmount: 500000m,
            areaSquareMeters: 20.0,
            roomType: "Khép kín",
            status: invalidStatus
        ));

        Assert.That(ex!.Message, Does.Contain("Invalid room status"));
    }

    [Test]
    public void Constructor_DefensiveCopy_PreventsExternalListMutationFromAlteringEntity()
    {
        // Arrange
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
            roomType: "Khép kín",
            amenities: mutableAmenities,
            photoUrls: mutablePhotos,
            houseRules: mutableRules
        );

        // Act - Mutate the source lists outside the entity
        mutableAmenities.Add("Bể bơi");
        mutablePhotos.Add("https://example.com/photo2.jpg");
        mutableRules.Add("Cho phép tiệc tùng");

        // Assert - The Room entity's internal collections must remain unchanged
        Assert.That(room.Amenities, Has.Count.EqualTo(2));
        Assert.That(room.Amenities, Does.Not.Contain("Bể bơi"));
        Assert.That(room.PhotoUrls, Has.Count.EqualTo(1));
        Assert.That(room.HouseRules, Has.Count.EqualTo(1));
    }

    [Test]
    public void Room_PendingVerificationStatus_HasExpectedFlags()
    {
        var room = new Room(
            id: "room-pending",
            title: "Phòng chờ duyệt",
            description: "Chưa được ban quản trị duyệt",
            monthlyRent: 1200000m,
            depositAmount: 500000m,
            areaSquareMeters: 18.0,
            roomType: "Khép kín",
            status: RoomStatus.PendingVerification
        );

        Assert.That(room.Status, Is.EqualTo(RoomStatus.PendingVerification));
        Assert.That(room.IsAvailable, Is.False);
        Assert.That(room.IsPendingVerification, Is.True);
        Assert.That(room.IsPubliclyVisible, Is.False);
    }

    [Test]
    public void SetConcurrencyToken_WithValidToken_UpdatesToken()
    {
        var room = new Room(
            id: "room-001",
            title: "Phòng trọ",
            description: "Mô tả",
            monthlyRent: 1500000m,
            depositAmount: 500000m,
            areaSquareMeters: 20.0,
            roomType: "Khép kín"
        );

        room.SetConcurrencyToken("new-etag-999");

        Assert.That(room.ConcurrencyToken, Is.EqualTo("new-etag-999"));
    }
}
