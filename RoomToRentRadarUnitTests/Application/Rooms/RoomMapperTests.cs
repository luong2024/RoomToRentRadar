#pragma warning disable CS0618
using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadarUnitTests.Application.Rooms;

[TestFixture]
public class RoomMapperTests
{
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
        Assert.That(detail.IsFavourite, Is.False);
        Assert.That(detail.PublishedAtUtc, Is.Not.Null);
    }

    [Test]
    public void ToDetailDto_WithoutHouseRules_ReturnsNullHouseRules()
    {
        var room = new Room(
            id: "room-no-rules",
            title: "Phòng trọ không nội quy",
            description: "Mô tả",
            monthlyRent: 1500000m,
            depositAmount: 500000m,
            areaSquareMeters: 20.0,
            roomType: "BoardingRoom",
            fullAddress: "Đường Z115"
        );

        var detail = RoomMapper.ToDetailDto(room, null);

        Assert.That(detail.HouseRules, Is.Null, "HouseRules must be null when room has no declared rules");
    }

    [Test]
    public void ToDetailDto_Utilities_Implements_BR_LST_03_Correctly()
    {
        // 1. All utilities null -> estimate equals base rent
        var roomBase = Room.Reconstitute(
            id: "room-base-rent",
            landlordId: "landlord-1",
            title: "Phòng trọ không kèm tiện ích",
            roomType: RoomType.BoardingRoom,
            monthlyRentVnd: 2_000_000L,
            depositVnd: 2_000_000L,
            areaM2: 25.0,
            addressLine: "Z115",
            ward: "Tân Thịnh",
            district: "TP. Thái Nguyên",
            latitude: 21.5875,
            longitude: 105.8080,
            status: ListingStatus.Available,
            createdAtUtc: DateTime.UtcNow,
            lastUpdatedAtUtc: DateTime.UtcNow,
            version: 1,
            concurrencyToken: "1"
        );

        var detailBase = RoomMapper.ToDetailDto(roomBase, null);
        Assert.That(detailBase.Utilities.EstimatedMonthlyTotalVnd, Is.EqualTo(2_000_000L));
        Assert.That(detailBase.Utilities.ElectricityPriceVndPerKwh, Is.Null);
        Assert.That(detailBase.Utilities.WaterPriceVnd, Is.Null);
        Assert.That(detailBase.Utilities.WaterPriceUnit, Is.Null);

        // 2. PerPerson water: rent + 100*elec + 1*water + internet + parking
        var roomPerPerson = Room.Reconstitute(
            id: "room-per-person",
            landlordId: "landlord-1",
            title: "Phòng trọ nước tính theo người",
            roomType: RoomType.BoardingRoom,
            monthlyRentVnd: 2_000_000L,
            depositVnd: 2_000_000L,
            areaM2: 25.0,
            addressLine: "Z115",
            ward: "Tân Thịnh",
            district: "TP. Thái Nguyên",
            latitude: 21.5875,
            longitude: 105.8080,
            status: ListingStatus.Available,
            createdAtUtc: DateTime.UtcNow,
            lastUpdatedAtUtc: DateTime.UtcNow,
            version: 1,
            concurrencyToken: "1",
            electricityPriceVndPerKwh: 3_500L,
            waterPriceVnd: 50_000L,
            waterPriceUnit: "PerPerson",
            internetFeeVnd: 80_000L,
            parkingFeeVnd: 50_000L
        );

        var detailPerPerson = RoomMapper.ToDetailDto(roomPerPerson, null);
        // 2_000_000 + (100 * 3500) + (1 * 50_000) + 80_000 + 50_000 = 2_000_000 + 350_000 + 50_000 + 80_000 + 50_000 = 2_530_000
        Assert.That(detailPerPerson.Utilities.EstimatedMonthlyTotalVnd, Is.EqualTo(2_530_000L));
        Assert.That(detailPerPerson.Utilities.WaterPriceUnit, Is.EqualTo("PerPerson"));

        // 3. Included water: 0 for water
        var roomIncluded = Room.Reconstitute(
            id: "room-included",
            landlordId: "landlord-1",
            title: "Phòng trọ miễn phí nước",
            roomType: RoomType.BoardingRoom,
            monthlyRentVnd: 2_000_000L,
            depositVnd: 2_000_000L,
            areaM2: 25.0,
            addressLine: "Z115",
            ward: "Tân Thịnh",
            district: "TP. Thái Nguyên",
            latitude: 21.5875,
            longitude: 105.8080,
            status: ListingStatus.Available,
            createdAtUtc: DateTime.UtcNow,
            lastUpdatedAtUtc: DateTime.UtcNow,
            version: 1,
            concurrencyToken: "1",
            waterPriceVnd: 0L,
            waterPriceUnit: "Included"
        );

        var detailIncluded = RoomMapper.ToDetailDto(roomIncluded, null);
        Assert.That(detailIncluded.Utilities.EstimatedMonthlyTotalVnd, Is.EqualTo(2_000_000L));
    }
}
