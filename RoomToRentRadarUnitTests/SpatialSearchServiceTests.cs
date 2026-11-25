using FluentAssertions;
using RoomToRentRadar.Api.Repositories;
using Xunit;
using ApiSpatial = RoomToRentRadar.Api.Services.SpatialSearchService;
using Request = RoomToRentRadar.Shared.Dtos.SpatialSearchRequestDto;

namespace RoomToRentRadarUnitTests;

public class SpatialSearchServiceTests
{
    private readonly ApiSpatial _sut = new(new InMemoryListingRepository());

    // Hình chữ nhật bao quanh ICTU (chứa toàn bộ seed data), GeoJSON [lng, lat].
    private static readonly List<double[]> CampusBox =
    [
        [105.800, 21.580], [105.815, 21.580], [105.815, 21.592], [105.800, 21.592], [105.800, 21.580]
    ];

    [Fact]
    public async Task Search_PolygonAroundCampus_ReturnsPublicListingsSortedByDistance()
    {
        var result = await _sut.SearchInPolygonAsync(new Request { Polygon = CampusBox });

        result.TotalCount.Should().BeGreaterThan(0);
        result.Items.Should().HaveCount(result.TotalCount);
        result.Items.Should().OnlyContain(i => i.IsVerified);
        result.Items.Should().NotContain(i => i.Id == "room-2026-004");
        result.Items.Select(i => i.DistanceMeters).Should().BeInAscendingOrder();
        result.Items.Should().OnlyContain(i => i.Latitude > 21 && i.Longitude > 105);
    }

    [Fact]
    public async Task Search_PolygonFarAway_ReturnsEmpty()
    {
        var hanoi = new List<double[]> { new[] { 105.80, 21.00 }, new[] { 105.85, 21.00 }, new[] { 105.85, 21.05 } };

        var result = await _sut.SearchInPolygonAsync(new Request { Polygon = hanoi });

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_SortByRent_ReturnsAscendingRent()
    {
        var result = await _sut.SearchInPolygonAsync(new Request { Polygon = CampusBox, SortBy = "rent" });

        result.Items.Select(i => i.Rent).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Search_TooFewPoints_Throws()
    {
        var act = () => _sut.SearchInPolygonAsync(new Request { Polygon = [[105.80, 21.58], [105.81, 21.58]] });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void Normalize_OpenRing_AutoClosesFirstPoint()
    {
        var ring = ApiSpatial.Normalize([[105.80, 21.58], [105.81, 21.58], [105.81, 21.59]]);

        ring.Should().HaveCount(4);
        ring[^1].Should().Equal(ring[0]);
    }

    [Fact]
    public async Task Search_SortByPriceAlias_MatchesRentSort()
    {
        var result = await _sut.SearchInPolygonAsync(new Request { Polygon = CampusBox, SortBy = "price" });

        result.Items.Select(i => i.Rent).Should().BeInAscendingOrder();
    }

    [Fact]
    public void Normalize_DuplicatePointsOnly_Throws()
    {
        var act = () => ApiSpatial.Normalize([[105.80, 21.58], [105.80, 21.58], [105.81, 21.58], [105.80, 21.58]]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Normalize_InvalidCoordinate_Throws()
    {
        var act = () => ApiSpatial.Normalize([[200, 21.58], [105.81, 21.58], [105.81, 21.59]]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsInside_PointOutside_ReturnsFalse()
    {
        ApiSpatial.IsInside([106.0, 22.0], CampusBox).Should().BeFalse();
        ApiSpatial.IsInside([105.8066, 21.5850], CampusBox).Should().BeTrue();
    }
}
