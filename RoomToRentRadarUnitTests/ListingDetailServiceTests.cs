using FluentAssertions;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services;
using Xunit;

namespace RoomToRentRadarUnitTests;

public class ListingDetailServiceTests
{
    private readonly ListingDetailService _sut = new(new InMemoryListingRepository());

    [Fact]
    public async Task GetById_AvailableListing_ReturnsDescriptionAmenitiesAndRules()
    {
        var detail = await _sut.GetByIdAsync("room-2026-001", isAuthenticated: true);

        detail.Should().NotBeNull();
        detail!.IsAvailable.Should().BeTrue();
        detail.UnavailableMessage.Should().BeNull();
        detail.Title.Should().Contain("ICTU");
        detail.Description.Should().Contain("an ninh vân tay");
        detail.Amenities.Should().Contain(["Wifi", "PrivateWC", "FingerprintLock"]);
        detail.HouseRules.QuietHours.Should().Be("23:00 - 05:30");
        detail.HouseRules.Version.Should().Be(2);
        detail.Rent.Should().Be(1_800_000m);
        detail.Deposit.Should().Be(1_800_000m);
        detail.Area.Should().Be(22.5);
        detail.RoomType.Should().Be("Studio");
        detail.SimilarRooms.Should().BeEmpty();
        detail.Landlord.Name.Should().Contain("Lan");
        detail.Landlord.Phone.Should().Be("0987654321");
    }

    [Fact]
    public async Task GetById_DungAliasId_ResolvesSameRoomAsQuangAnhCanonicalId()
    {
        var byCanonical = await _sut.GetByIdAsync("room-2026-001", isAuthenticated: true);
        var byDungAlias = await _sut.GetByIdAsync("room-ictu-001", isAuthenticated: true);

        byCanonical.Should().NotBeNull();
        byDungAlias.Should().NotBeNull();
        byDungAlias!.Title.Should().Be(byCanonical!.Title);
        byDungAlias.Rent.Should().Be(byCanonical.Rent);
        byDungAlias.Id.Should().Be("room-ictu-001");
    }

    [Fact]
    public async Task GetById_Unauthenticated_MasksLandlordPhone()
    {
        var detail = await _sut.GetByIdAsync("room-2026-001", isAuthenticated: false);

        detail.Should().NotBeNull();
        detail!.Landlord.Phone.Should().Be("098*** (Sign in to view)");
        detail.Landlord.Name.Should().Contain("Lan");
    }

    [Fact]
    public async Task GetById_Unavailable_ReturnsMessageAndTop3SimilarRooms()
    {
        var detail = await _sut.GetByIdAsync("room-2026-004", isAuthenticated: false);

        detail.Should().NotBeNull();
        detail!.IsAvailable.Should().BeFalse();
        detail.Status.Should().Be("Rented");
        detail.UnavailableMessage.Should().Be(ListingDetailService.UnavailableMessage);
        detail.SimilarRooms.Should().HaveCount(3);
        detail.SimilarRooms.Should().OnlyContain(r => r.IsVerified);
        detail.SimilarRooms.Select(r => r.Id).Should().NotContain("room-2026-004");
        detail.SimilarRooms.Take(2).Should().OnlyContain(r => r.RoomType == "Studio");
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNull()
    {
        var detail = await _sut.GetByIdAsync("missing-id", isAuthenticated: false);
        detail.Should().BeNull();
    }

    [Xunit.Theory]
    [InlineData("0987654321", "098*** (Sign in to view)")]
    [InlineData("0912-345-678", "091*** (Sign in to view)")]
    [InlineData("", "*** (Sign in to view)")]
    public void MaskPhone_KeepsPrefixAndAppendsSignInHint(string raw, string expected)
    {
        ListingDetailService.MaskPhone(raw).Should().Be(expected);
    }
}
