#pragma warning disable CS0618
using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadarUnitTests.Application.Rooms;

[TestFixture]
public class RoomMapperTests
{
    [TestCase(ListingStatus.Available, "Available")]
    [TestCase(ListingStatus.Rented, "Rented")]
    [TestCase(ListingStatus.Unpublished, "Unavailable")]
    [TestCase(ListingStatus.PendingReview, "PendingVerification")]
    [TestCase(ListingStatus.Rejected, "Unavailable")]
    [TestCase(ListingStatus.Suspended, "Unavailable")]
    public void MapRoomStatus_MapsListingStatusValuesToExplicitStableStrings(ListingStatus status, string expectedString)
    {
        var mapped = RoomMapper.MapRoomStatus(status);
        Assert.That(mapped, Is.EqualTo(expectedString));
    }

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
            roomType: "BoardingRoom",
            fullAddress: "Ngõ 1 Đường Z115",
            photoUrls: new[] { "https://cdn.example.com/thumb.jpg" },
            isVerified: true,
            distanceToCampusKm: 0.5
        );

        var card = RoomMapper.ToCardDto(room);

        Assert.That(card.Id, Is.EqualTo("room-c-01"));
        Assert.That(card.Title, Is.EqualTo("Phòng trọ mini"));
        // M3: canonical OpenAPI field names
        Assert.That(card.MonthlyRentVnd, Is.EqualTo(1500000m));
        Assert.That(card.AreaM2, Is.EqualTo(20.0));
        Assert.That(card.DistanceKm, Is.EqualTo(0.5));
        Assert.That(card.IsVerified, Is.True);
        Assert.That(card.CoverThumbUrl, Is.EqualTo("https://cdn.example.com/thumb.jpg"));
        Assert.That(card.RoomType, Is.EqualTo(RoomType.BoardingRoom));
        Assert.That(card.Status, Is.EqualTo(ListingStatus.Available));
        Assert.That(card.IsFavourite, Is.False);
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
            roomType: "MiniApartment",
            amenities: new[] { "AirConditioner", "WaterHeater" },
            fullAddress: "Số 99 Đường Z115",
            landlordName: "Bác An",
            landlordPhoneNumber: "0912345678",
            photoUrls: new[] { "https://cdn.example.com/p1.jpg" },
            panorama360Url: "https://my.matterport.com/show/?m=xyz",
            houseRules: new[] { "Không nuôi thú cưng" },
            status: ListingStatus.Available,
            isVerified: true,
            distanceToCampusKm: 0.4
        );

        var detail = RoomMapper.ToDetailDto(
            room,
            landlordPhoneNumber: "0912345678");

        Assert.That(detail.Id, Is.EqualTo("room-d-01"));
        Assert.That(detail.Status, Is.EqualTo(ListingStatus.Available));
        // M3: use canonical OpenAPI field names — no aliases
        Assert.That(detail.Status == ListingStatus.Available, Is.True, "Status should be Available");
        Assert.That(detail.Landlord.PhoneNumber, Is.EqualTo("0912345678"));
        Assert.That(detail.Amenities, Has.Count.EqualTo(2));
        Assert.That(detail.Utilities.EstimatedMonthlyTotalVnd, Is.GreaterThan(0));
        Assert.That(detail.Photos, Has.Count.EqualTo(2)); // Standard + Panorama
        Assert.That(detail.HouseRules, Is.Not.Null);
    }
}
