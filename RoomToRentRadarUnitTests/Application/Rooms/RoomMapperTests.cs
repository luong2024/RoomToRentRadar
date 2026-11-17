namespace RoomToRentRadarUnitTests.Application.Rooms;

using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;

[TestFixture]
public class RoomMapperTests
{
    private static readonly Campus Ictu = new(
        "ICTU", "Trường Đại học Công nghệ Thông tin và Truyền thông", "ICTU", 21.5851, 105.8066, isDefault: true, sortOrder: 1);

    private static User CreateSampleLandlord(string id = "landlord-1", string phone = "0987654321")
    {
        return User.RegisterLocal(
            email: new EmailAddress("landlord@example.com"),
            passwordHash: "hash-secret",
            fullName: "Nguyễn Văn Chủ Trọ",
            role: UserRole.Landlord,
            phoneNumber: phone);
    }

    private static Room CreateSampleRoom(
        string id = "room-1",
        ListingStatus status = ListingStatus.Available,
        long monthlyRent = 2_000_000L,
        long? electricity = 3_500L,
        long? waterPrice = 30_000L,
        string? waterUnit = "PerPerson",
        long? internet = 100_000L,
        long? parking = 50_000L,
        int? maxOccupants = 2)
    {
        return Room.Reconstitute(
            id: id,
            landlordId: "landlord-1",
            title: "Phòng trọ cao cấp gần cổng trường ICTU",
            roomType: RoomType.BoardingRoom,
            monthlyRentVnd: monthlyRent,
            depositVnd: monthlyRent,
            areaM2: 25.0,
            addressLine: "Số 123 Đường Z115",
            ward: "Tân Thịnh",
            district: "TP. Thái Nguyên",
            latitude: 21.5875,
            longitude: 105.8080,
            status: status,
            createdAtUtc: new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            lastUpdatedAtUtc: new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc),
            version: 1,
            description: "Phòng sạch sẽ, an ninh tốt, giờ giấc tự do.",
            maxOccupants: maxOccupants,
            furnishing: "Full",
            availableFrom: new DateOnly(2026, 11, 15),
            electricityPriceVndPerKwh: electricity,
            waterPriceVnd: waterPrice,
            waterPriceUnit: waterUnit,
            internetFeeVnd: internet,
            parkingFeeVnd: parking,
            otherFeesNote: "Vệ sinh 30.000 đ/tháng",
            verifiedAtUtc: new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc),
            publishedAtUtc: new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void ToDetailDto_WhenGivenValidRoomAndLandlord_MapsAllFieldsCorrectly()
    {
        // Arrange
        var room = CreateSampleRoom();
        var landlord = CreateSampleLandlord();

        // Act
        var dto = RoomMapper.ToDetailDto(room, landlord, Ictu, isPhoneVisible: true);

        // Assert
        Assert.That(dto.Id, Is.EqualTo("room-1"));
        Assert.That(dto.Title, Is.EqualTo("Phòng trọ cao cấp gần cổng trường ICTU"));
        Assert.That(dto.Description, Is.EqualTo("Phòng sạch sẽ, an ninh tốt, giờ giấc tự do."));
        Assert.That(dto.RoomType, Is.EqualTo(RoomType.BoardingRoom));
        Assert.That(dto.Status, Is.EqualTo(ListingStatus.Available));
        Assert.That(dto.IsVerified, Is.True);
        Assert.That(dto.MonthlyRentVnd, Is.EqualTo(2_000_000L));
        Assert.That(dto.DepositVnd, Is.EqualTo(2_000_000L));
        Assert.That(dto.AreaM2, Is.EqualTo(25.0));
        Assert.That(dto.Address.AddressLine, Is.EqualTo("Số 123 Đường Z115"));
        Assert.That(dto.Address.Ward, Is.EqualTo("Tân Thịnh"));
        Assert.That(dto.Address.District, Is.EqualTo("TP. Thái Nguyên"));
        Assert.That(dto.Location.Latitude, Is.EqualTo(21.5875));
        Assert.That(dto.Location.Longitude, Is.EqualTo(105.8080));
        Assert.That(dto.MaxOccupants, Is.EqualTo(2));
        Assert.That(dto.Furnishing, Is.EqualTo("Full"));
        Assert.That(dto.AvailableFrom, Is.EqualTo("2026-11-15"));
        Assert.That(dto.Landlord.DisplayName, Is.EqualTo("Nguyễn Văn Chủ Trọ"));
        Assert.That(dto.Landlord.PhoneNumber, Is.EqualTo("0987654321"));
        Assert.That(dto.Landlord.ZaloUrl, Is.EqualTo("https://zalo.me/0987654321"));
    }

    [Test]
    public void ToDetailDto_WhenPhoneNotVisible_MasksLandlordPhoneNumberAndZaloUrl()
    {
        // Arrange (AC4 / BR-AUTH-07)
        var room = CreateSampleRoom();
        var landlord = CreateSampleLandlord(phone: "0987654321");

        // Act
        var dto = RoomMapper.ToDetailDto(room, landlord, Ictu, isPhoneVisible: false);

        // Assert
        Assert.That(dto.Landlord.PhoneNumber, Is.Null);
        Assert.That(dto.Landlord.ZaloUrl, Is.Null);
        Assert.That(dto.Landlord.DisplayName, Is.EqualTo("Nguyễn Văn Chủ Trọ"));
    }

    [Test]
    public void ToDetailDto_CalculatesUtilityEstimatePerPerson_CorrectlyPerBrLst03()
    {
        // Arrange: Rent = 2,000,000 | 100 kWh * 3,500 = 350,000 | Water PerPerson: 30,000 * 2 = 60,000 | Net = 100,000 | Parking = 50,000
        // Total expected = 2,000,000 + 350,000 + 60,000 + 100,000 + 50,000 = 2,560,000
        var room = CreateSampleRoom(
            monthlyRent: 2_000_000L,
            electricity: 3_500L,
            waterPrice: 30_000L,
            waterUnit: "PerPerson",
            internet: 100_000L,
            parking: 50_000L,
            maxOccupants: 2);
        var landlord = CreateSampleLandlord();

        // Act
        var dto = RoomMapper.ToDetailDto(room, landlord, Ictu, isPhoneVisible: false);

        // Assert
        Assert.That(dto.Utilities.EstimatedMonthlyTotalVnd, Is.EqualTo(2_560_000L));
    }

    [Test]
    public void ToDetailDto_CalculatesUtilityEstimatePerCubicMeter_CorrectlyPerBrLst03()
    {
        // Arrange: Rent = 2,000,000 | 100 kWh * 3,500 = 350,000 | Water PerCubicMeter: 4 m3 * 25,000 = 100,000 | Net = 0 | Parking = 0
        // Total expected = 2,000,000 + 350,000 + 100,000 = 2,450,000
        var room = CreateSampleRoom(
            monthlyRent: 2_000_000L,
            electricity: 3_500L,
            waterPrice: 25_000L,
            waterUnit: "PerCubicMeter",
            internet: null,
            parking: null);
        var landlord = CreateSampleLandlord();

        // Act
        var dto = RoomMapper.ToDetailDto(room, landlord, Ictu, isPhoneVisible: false);

        // Assert
        Assert.That(dto.Utilities.EstimatedMonthlyTotalVnd, Is.EqualTo(2_450_000L));
    }

    [Test]
    public void ToDetailDto_CalculatesUtilityEstimateIncluded_CorrectlyPerBrLst03()
    {
        // Arrange: Water Included -> water added = 0
        var room = CreateSampleRoom(
            monthlyRent: 2_000_000L,
            electricity: 3_000L,
            waterPrice: 0L,
            waterUnit: "Included",
            internet: 50_000L,
            parking: null);
        var landlord = CreateSampleLandlord();

        // Act
        var dto = RoomMapper.ToDetailDto(room, landlord, Ictu, isPhoneVisible: false);

        // Assert: 2,000,000 + 300,000 + 0 + 50,000 = 2,350,000
        Assert.That(dto.Utilities.EstimatedMonthlyTotalVnd, Is.EqualTo(2_350_000L));
    }
}
