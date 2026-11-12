using FluentAssertions;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services;
using RoomToRentRadar.Shared.Dtos;
using Xunit;

namespace RoomToRentRadarUnitTests;

public class ListingSearchServiceTests
{
    private readonly ListingSearchService _sut = new(new InMemoryListingRepository());

    [Fact]
    public async Task Search_Default_ReturnsOnlyAvailableVerifiedSortedByDistance()
    {
        var result = await _sut.SearchAsync(new ListingSearchQuery
        {
            CampusId = "ictu-main",
            SortBy = "distance"
        });

        result.TotalCount.Should().BeGreaterThan(0);
        result.Items.Should().OnlyContain(i => i.Status == "Available" && i.IsVerified);
        result.Items.Should().NotContain(i => i.Id == "room-2026-004");
        result.Items.Select(i => i.DistanceMeters).Should().BeInAscendingOrder();
        result.ActiveFilters.SortBy.Should().Be("distance");
    }

    [Fact]
    public async Task Search_RentAndAreaAndDistanceFilters_ReturnsMatchingRooms()
    {
        var result = await _sut.SearchAsync(new ListingSearchQuery
        {
            CampusId = "ictu-main",
            MinRent = 1_600_000m,
            MaxRent = 2_000_000m,
            MinArea = 18,
            MaxArea = 25,
            MaxDistanceMeters = 600,
            RoomType = "Studio"
        });

        result.Items.Should().OnlyContain(i =>
            i.Rent >= 1_600_000m
            && i.Rent <= 2_000_000m
            && i.Area >= 18
            && i.Area <= 25
            && i.DistanceMeters <= 600
            && i.RoomType == "Studio");
        result.ActiveFilters.MinRent.Should().Be(1_600_000m);
        result.ActiveFilters.MaxRent.Should().Be(2_000_000m);
        result.ActiveFilters.RoomType.Should().Be("Studio");
        result.TotalCount.Should().Be(result.Items.Count);
    }

    [Fact]
    public async Task Search_NoMatch_ReturnsEmptyWithActiveFilters()
    {
        var result = await _sut.SearchAsync(new ListingSearchQuery
        {
            CampusId = "ictu-main",
            MinRent = 9_000_000m,
            MaxRent = 10_000_000m
        });

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
        result.HasNextPage.Should().BeFalse();
        result.ActiveFilters.MinRent.Should().Be(9_000_000m);
    }
}
