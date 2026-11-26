using FluentAssertions;
using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Services.Search;
using RoomToRentRadar.Shared.Dtos;
using Xunit;

namespace RoomToRentRadarUnitTests;

/// <summary>Refactor US04 / T04.1 — bảng sắp xếp và phân trang dùng chung.</summary>
public class ListingSorterTests
{
    private static readonly List<ListingDocument> Listings =
    [
        new() { Id = "a", Rent = 2_000_000m, Area = 15, DistanceMeters = 300 },
        new() { Id = "b", Rent = 1_500_000m, Area = 25, DistanceMeters = 900 },
        new() { Id = "c", Rent = 1_800_000m, Area = 20, DistanceMeters = 100 }
    ];

    [Theory]
    [InlineData(ListingSortKeys.Distance, "c,a,b")]
    [InlineData(null, "c,a,b")]
    [InlineData("unknown", "c,a,b")]
    [InlineData(ListingSortKeys.Price, "b,c,a")]
    [InlineData(ListingSortKeys.RentAsc, "b,c,a")]
    [InlineData("PRICE_DESC", "a,c,b")]
    [InlineData(ListingSortKeys.Area, "b,c,a")]
    public void Apply_SortsBySupportedKey(string? sortBy, string expectedIds)
    {
        ListingSorter.Apply(Listings, sortBy).Select(l => l.Id).Should().Equal(expectedIds.Split(','));
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(3, 500, 3, ListingSearchQuery.MaxPageSize)]
    [InlineData(2, 10, 2, 10)]
    public void Paging_ClampsIndexAndSize(int pageIndex, int pageSize, int expectedIndex, int expectedSize)
    {
        var paging = ListingPaging.From(new ListingSearchQuery { PageIndex = pageIndex, PageSize = pageSize });

        paging.PageIndex.Should().Be(expectedIndex);
        paging.PageSize.Should().Be(expectedSize);
    }

    [Fact]
    public void Paging_HasNextPage_WhenMoreItemsRemain()
    {
        var paging = new ListingPaging(1, 10);

        paging.HasNextPage(11).Should().BeTrue();
        paging.HasNextPage(10).Should().BeFalse();
        paging.Skip.Should().Be(0);
    }
}
