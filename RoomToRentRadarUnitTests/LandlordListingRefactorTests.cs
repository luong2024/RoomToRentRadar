using FluentAssertions;
using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Services.Landlord;
using RoomToRentRadar.Shared.Dtos;
using Xunit;

namespace RoomToRentRadarUnitTests;

/// <summary>Refactor US08 / T08.2 — id, khoảng cách campus và mapper tách khỏi LandlordListingService.</summary>
public class LandlordListingRefactorTests
{
    [Fact]
    public void NewListingId_HasRoomPrefixAndTwelveHexChars()
    {
        var id = LandlordListingService.NewListingId();

        id.Should().MatchRegex("^room-[0-9a-f]{12}$");
        LandlordListingService.NewListingId().Should().NotBe(id);
    }

    [Fact]
    public void DistanceFromCampus_UnknownCampus_UsesDefaultCampus()
    {
        double[] point = [105.8100, 21.5900];

        CampusLocations.DistanceFromCampusMeters("unknown", point)
            .Should().Be(CampusLocations.DistanceFromCampusMeters(CampusIds.Default, point));
        CampusLocations.DistanceFromCampusMeters(CampusIds.Default, CampusLocations.Get(null)).Should().Be(0);
    }

    [Fact]
    public void Mapper_OrdersPhotosAndUsesThumbnailAsCover()
    {
        var listing = new ListingDocument
        {
            Id = "room-1",
            Status = ListingAvailabilityStatus.PendingReview,
            Location = new GeoJsonPoint { Coordinates = [105.81, 21.59] },
            Photos =
            [
                new PhotoDocument { Id = "p2", Order = 2, Url = "u2" },
                new PhotoDocument { Id = "p1", Order = 1, Url = "u1", ThumbnailUrl = "t1" }
            ]
        };

        var dto = LandlordListingMapper.ToDto(listing);

        dto.Photos.Select(p => p.Id).Should().Equal("p1", "p2");
        dto.PhotoCount.Should().Be(2);
        dto.CoverPhotoUrl.Should().Be("t1");
        dto.Longitude.Should().Be(105.81);
        dto.Latitude.Should().Be(21.59);
    }

    [Fact]
    public void BuildDocument_TrimsHouseRulesAndDefaultsCampus()
    {
        var request = new CreateListingRequestDto
        {
            CampusId = " ",
            Title = " Phòng ",
            Description = " Mô tả ",
            RoomType = " Phòng đơn ",
            Address = " Số 1 ",
            ContactName = " Chủ trọ ",
            ContactPhone = "0912345678",
            QuietHours = " 22:00 ",
            Longitude = 105.8066,
            Latitude = 21.5850
        };

        var doc = LandlordListingService.BuildDocument("landlord-1", request, DateTimeOffset.UnixEpoch);

        doc.CampusId.Should().Be(CampusIds.Default);
        doc.HouseRules.Version.Should().Be(1);
        doc.HouseRules.QuietHours.Should().Be("22:00");
        doc.HouseRules.GuestPolicy.Should().BeEmpty();
        doc.Landlord.Id.Should().Be("landlord-1");
        doc.Landlord.Name.Should().Be("Chủ trọ");
        doc.DistanceMeters.Should().Be(0);
    }
}
