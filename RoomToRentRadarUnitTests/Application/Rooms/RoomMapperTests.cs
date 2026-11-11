using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadarUnitTests.Application.Rooms;

[TestFixture]
public class RoomMapperTests
{
    [TestCase(RoomStatus.Available, "Available")]
    [TestCase(RoomStatus.Unavailable, "Unavailable")]
    [TestCase(RoomStatus.Rented, "Rented")]
    [TestCase(RoomStatus.PendingVerification, "PendingVerification")]
    public void MapRoomStatus_MapsEnumValuesToExplicitStableStrings(RoomStatus status, string expectedString)
    {
        var mapped = RoomMapper.MapRoomStatus(status);
        Assert.That(mapped, Is.EqualTo(expectedString));
    }

    [Test]
    public void ToCardDto_MapsPropertiesAccurately()
    {
        var room = new Room(
            id: "room-c-01",
            title: "Phòng trọ mini",
            description: "Mô tả",
            monthlyRent: 1500000m,
            depositAmount: 500000m,
            areaSquareMeters: 20.0,
            roomType: "Khép kín",
            fullAddress: "Ngõ 1 Đường Z115",
            photoUrls: new[] { "https://cdn.example.com/thumb.jpg" },
            isVerified: true,
            distanceToCampusKm: 0.5
        );

        var card = RoomMapper.ToCardDto(room);

        Assert.That(card.Id, Is.EqualTo("room-c-01"));
        Assert.That(card.Title, Is.EqualTo("Phòng trọ mini"));
        Assert.That(card.MonthlyRent, Is.EqualTo(1500000m));
        Assert.That(card.AreaSquareMeters, Is.EqualTo(20.0));
        Assert.That(card.DistanceToCampusKm, Is.EqualTo(0.5));
        Assert.That(card.IsVerified, Is.True);
        Assert.That(card.ThumbnailUrl, Is.EqualTo("https://cdn.example.com/thumb.jpg"));
        Assert.That(card.AddressSummary, Is.EqualTo("Ngõ 1 Đường Z115"));
    }

    [Test]
    public void ToDetailDto_MapsAllPropertiesAndPreservesInvariants()
    {
        var room = new Room(
            id: "room-d-01",
            title: "Phòng trọ VIP",
            description: "Mô tả chi tiết",
            monthlyRent: 2500000m,
            depositAmount: 1500000m,
            areaSquareMeters: 30.0,
            roomType: "Chung cư mini",
            amenities: new[] { "Điều hòa", "Nóng lạnh" },
            fullAddress: "Số 99 Đường Z115",
            landlordName: "Bác An",
            landlordPhoneNumber: "0912345678",
            photoUrls: new[] { "https://cdn.example.com/p1.jpg" },
            panorama360Url: "https://my.matterport.com/show/?m=xyz",
            houseRules: new[] { "Không nuôi thú cưng" },
            status: RoomStatus.Available,
            isVerified: true,
            distanceToCampusKm: 0.4
        );

        var similarRooms = new List<RoomCardDto>
        {
            new("sim-1", "Phòng 1", 2400000m, 28.0, 0.5, true, "thumb1", "address1")
        };

        var detail = RoomMapper.ToDetailDto(
            room,
            landlordPhoneNumber: "0912345678",
            unavailableMessage: null,
            similarRooms: similarRooms);

        Assert.That(detail.Id, Is.EqualTo("room-d-01"));
        Assert.That(detail.Status, Is.EqualTo("Available"));
        Assert.That(detail.IsAvailable, Is.True);
        Assert.That(detail.LandlordPhoneNumber, Is.EqualTo("0912345678"));
        Assert.That(detail.Amenities, Has.Count.EqualTo(2));
        Assert.That(detail.HouseRules, Has.Count.EqualTo(1));
        Assert.That(detail.SimilarRooms, Has.Count.EqualTo(1));
    }
}
