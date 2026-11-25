using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Controllers;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services;
using RoomToRentRadar.Shared.Dtos;
using RoomToRentRadarUnitTests.TestData;
using Xunit;

namespace RoomToRentRadarUnitTests;

public class SearchFilterCombinationTests
{
    private readonly InMemoryListingRepository _repo =
        new(SearchListingTestDataFactory.CreateFilterMatrixDataset());

    private readonly ListingSearchService _sut;

    public SearchFilterCombinationTests()
    {
        _sut = new ListingSearchService(_repo);
    }

    [Fact]
    public async Task Search_RentBoundaryFilters_AreInclusiveAndExcludeOutOfRange()
    {
        var minOnly = await _sut.SearchAsync(new ListingSearchQuery { MinRent = 2_500_000m });
        minOnly.Items.Should().OnlyContain(i => i.Rent >= 2_500_000m);
        minOnly.Items.Select(i => i.Id).Should().BeEquivalentTo("room-2026-003", "room-2026-006", "room-2026-007");

        var maxOnly = await _sut.SearchAsync(new ListingSearchQuery { MaxRent = 1_500_000m });
        maxOnly.Items.Should().OnlyContain(i => i.Rent <= 1_500_000m);
        maxOnly.Items.Select(i => i.Id).Should().BeEquivalentTo(
            "room-2026-001", "room-2026-004", "room-2026-008", "room-2026-009");
    }

    [Fact]
    public async Task Search_AreaBoundaryFilters_AreInclusive()
    {
        var result = await _sut.SearchAsync(new ListingSearchQuery
        {
            MinArea = 25.0,
            MaxArea = 30.0
        });

        result.Items.Should().OnlyContain(i => i.Area >= 25.0 && i.Area <= 30.0);
        result.Items.Select(i => i.Id).Should().BeEquivalentTo(
            "room-2026-002", "room-2026-003", "room-2026-005", "room-2026-011");
    }

    [Fact]
    public async Task Search_MaxDistanceBoundary_IsInclusive()
    {
        var result = await _sut.SearchAsync(new ListingSearchQuery
        {
            MaxDistanceMeters = 500
        });

        result.Items.Should().OnlyContain(i => i.DistanceMeters <= 500);
        result.Items.Select(i => i.Id).Should().Contain(["room-2026-001", "room-2026-010"]);
        result.Items.Should().NotContain(i => i.Id == "room-2026-005");
    }

    [Xunit.Theory]
    [InlineData("studio", 5)]
    [InlineData("STUDIO", 5)]
    [InlineData("Khép kín", 2)]
    [InlineData("khép kín", 2)]
    [InlineData("Chung cư mini", 2)]
    [InlineData("Ở ghép", 2)]
    public async Task Search_RoomType_MatchesCaseInsensitively(string roomType, int expectedCount)
    {
        var result = await _sut.SearchAsync(new ListingSearchQuery { RoomType = roomType });

        result.TotalCount.Should().Be(expectedCount);
        result.Items.Should().OnlyContain(i =>
            string.Equals(i.RoomType, roomType, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Search_CampusId_IsolatesResultsBetweenCampuses()
    {
        var main = await _sut.SearchAsync(new ListingSearchQuery { CampusId = "ictu-main" });
        var cs2 = await _sut.SearchAsync(new ListingSearchQuery { CampusId = "ictu-cs2" });

        main.TotalCount.Should().Be(11);
        main.Items.Should().OnlyContain(i => i.CampusId == "ictu-main");

        cs2.TotalCount.Should().Be(2);
        cs2.Items.Select(i => i.Id).Should().BeEquivalentTo("room-2026-015", "room-2026-016");
    }

    public static TheoryData<string, ListingSearchQuery, string[]> CombinedFilterCases() => new()
    {
        {
            "Price + Area",
            new ListingSearchQuery { MinRent = 1_500_000m, MaxRent = 2_200_000m, MinArea = 22.0, MaxArea = 28.0 },
            ["room-2026-002", "room-2026-004", "room-2026-005", "room-2026-011"]
        },
        {
            "Price + Distance + RoomType",
            new ListingSearchQuery { MinRent = 1_500_000m, MaxRent = 2_000_000m, MaxDistanceMeters = 500, RoomType = "studio" },
            ["room-2026-001", "room-2026-010", "room-2026-011"]
        },
        {
            "Area + Distance + RoomType",
            new ListingSearchQuery { MinArea = 20.0, MaxArea = 30.0, MaxDistanceMeters = 1000, RoomType = "Khép kín" },
            ["room-2026-004", "room-2026-005"]
        },
        {
            "Full 6-Filter Combination",
            new ListingSearchQuery
            {
                CampusId = "ictu-main",
                MinRent = 1_500_000m,
                MaxRent = 2_000_000m,
                MinArea = 18.0,
                MaxArea = 25.0,
                RoomType = "Studio",
                MaxDistanceMeters = 1000,
                SortBy = "price"
            },
            ["room-2026-001", "room-2026-010", "room-2026-011", "room-2026-002"]
        }
    };

    [Xunit.Theory]
    [MemberData(nameof(CombinedFilterCases))]
    public async Task Search_CombinedFilters_ReturnsExactMatchesAndEchoesActiveFilters(
        string _,
        ListingSearchQuery query,
        string[] expectedIds)
    {
        var result = await _sut.SearchAsync(query);

        result.TotalCount.Should().Be(expectedIds.Length);
        result.Items.Select(i => i.Id).Should().BeEquivalentTo(expectedIds);
        result.Items.Should().OnlyContain(i => i.Status == "Available" && i.IsVerified);
        result.ActiveFilters.CampusId.Should().Be(query.CampusId);
        result.ActiveFilters.MinRent.Should().Be(query.MinRent);
        result.ActiveFilters.MaxRent.Should().Be(query.MaxRent);
        result.ActiveFilters.MinArea.Should().Be(query.MinArea);
        result.ActiveFilters.MaxArea.Should().Be(query.MaxArea);
        result.ActiveFilters.RoomType.Should().Be(query.RoomType);
        result.ActiveFilters.MaxDistanceMeters.Should().Be(query.MaxDistanceMeters);
    }

    [Xunit.Theory]
    [InlineData("distance", "distance")]
    [InlineData("", "distance")]
    [InlineData("   ", "distance")]
    [InlineData("unknown", "unknown")]
    public async Task Search_DefaultAndDistanceSort_OrdersByDistanceAscThenRentAsc(
        string sortBy,
        string expectedActiveSortBy)
    {
        var result = await _sut.SearchAsync(new ListingSearchQuery { SortBy = sortBy });

        result.ActiveFilters.SortBy.Should().Be(expectedActiveSortBy);
        result.Items.Should().BeInAscendingOrder(i => i.DistanceMeters);

        // Tại cùng khoảng cách 500m: room-2026-001 (1.5M) phải đứng trước room-2026-010 (1.8M)
        var at500m = result.Items.Where(i => i.DistanceMeters == 500).Select(i => i.Id).ToList();
        at500m.Should().ContainInOrder("room-2026-001", "room-2026-010");
    }

    [Xunit.Theory]
    [InlineData("price")]
    [InlineData("rent")]
    [InlineData("price_asc")]
    public async Task Search_PriceAscSort_OrdersByRentAscThenDistanceAsc(string sortBy)
    {
        var result = await _sut.SearchAsync(new ListingSearchQuery { SortBy = sortBy });

        result.Items.Should().BeInAscendingOrder(i => i.Rent);
        var at1500k = result.Items.Where(i => i.Rent == 1_500_000m).Select(i => i.Id).ToList();
        at1500k.Should().ContainInOrder("room-2026-004", "room-2026-001");
    }

    [Xunit.Theory]
    [InlineData("price_desc")]
    [InlineData("rent_desc")]
    public async Task Search_PriceDescSort_OrdersByRentDescThenDistanceAsc(string sortBy)
    {
        var result = await _sut.SearchAsync(new ListingSearchQuery { SortBy = sortBy });

        result.Items.Should().BeInDescendingOrder(i => i.Rent);
        var at2000k = result.Items.Where(i => i.Rent == 2_000_000m).Select(i => i.Id).ToList();
        at2000k.Should().ContainInOrder("room-2026-011", "room-2026-002");
    }

    [Xunit.Theory]
    [InlineData("area")]
    [InlineData("area_desc")]
    public async Task Search_AreaSort_OrdersByAreaDescThenDistanceAsc(string sortBy)
    {
        var result = await _sut.SearchAsync(new ListingSearchQuery { SortBy = sortBy });

        result.Items.Should().BeInDescendingOrder(i => i.Area);
        var at25m2 = result.Items.Where(i => i.Area == 25.0).Select(i => i.Id).ToList();
        at25m2.Should().ContainInOrder("room-2026-011", "room-2026-002");
    }

    [Fact]
    public async Task Search_MultiPageTraversal_CoversAllItemsWithoutDuplicates()
    {
        var collected = new List<string>();
        int page = 1;

        while (true)
        {
            var res = await _sut.SearchAsync(new ListingSearchQuery
            {
                PageIndex = page,
                PageSize = 3,
                SortBy = "distance"
            });

            collected.AddRange(res.Items.Select(i => i.Id));

            if (page < 4)
            {
                res.HasNextPage.Should().BeTrue();
                res.Items.Should().HaveCount(3);
            }
            else
            {
                res.HasNextPage.Should().BeFalse();
                res.Items.Should().HaveCount(2);
                collected.Should().HaveCount(res.TotalCount);
                collected.Should().OnlyHaveUniqueItems();
                break;
            }

            page++;
        }
    }

    [Fact]
    public async Task Search_PaginationEdgeCasesAndClamping_BehaveSafely()
    {
        var outOfRange = await _sut.SearchAsync(new ListingSearchQuery { PageIndex = 999, PageSize = 10 });
        outOfRange.TotalCount.Should().Be(11);
        outOfRange.Items.Should().BeEmpty();
        outOfRange.HasNextPage.Should().BeFalse();

        var clampedLow = await _sut.SearchAsync(new ListingSearchQuery { PageIndex = -3, PageSize = 0 });
        clampedLow.PageIndex.Should().Be(1);
        clampedLow.PageSize.Should().Be(1);
        clampedLow.Items.Should().HaveCount(1);
        clampedLow.HasNextPage.Should().BeTrue();

        var clampedHigh = await _sut.SearchAsync(new ListingSearchQuery { PageIndex = 1, PageSize = 200 });
        clampedHigh.PageSize.Should().Be(50);
        clampedHigh.Items.Should().HaveCount(11);
        clampedHigh.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public async Task Search_NeverExposesRentedPendingOrUnverifiedListings()
    {
        var result = await _sut.SearchAsync(new ListingSearchQuery { PageSize = 50 });

        result.Items.Select(i => i.Id).Should().NotContain(
            ["room-2026-012", "room-2026-013", "room-2026-014"]);
        result.Items.Should().OnlyContain(i => i.Status == "Available" && i.IsVerified);
    }

    [Fact]
    public async Task Search_InvertedRanges_ReturnEmptySet()
    {
        var invertedRent = await _sut.SearchAsync(new ListingSearchQuery { MinRent = 3_000_000m, MaxRent = 1_000_000m });
        invertedRent.TotalCount.Should().Be(0);
        invertedRent.Items.Should().BeEmpty();
        invertedRent.HasNextPage.Should().BeFalse();

        var invertedArea = await _sut.SearchAsync(new ListingSearchQuery { MinArea = 35, MaxArea = 15 });
        invertedArea.TotalCount.Should().Be(0);
        invertedArea.Items.Should().BeEmpty();
        invertedArea.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public async Task Search_ClearFilters_RestoresFullPublicCatalogAndMapsThumbnails()
    {
        var filtered = await _sut.SearchAsync(new ListingSearchQuery { RoomType = "Chung cư mini" });
        filtered.TotalCount.Should().Be(2);

        var cleared = await _sut.SearchAsync(new ListingSearchQuery());
        cleared.TotalCount.Should().Be(11);

        var orderedPhotoRoom = cleared.Items.Single(i => i.Id == "room-2026-001");
        orderedPhotoRoom.ThumbnailUrl.Should().Be("https://cdn.example/p1_a.jpg");

        var noPhotoRoom = cleared.Items.Single(i => i.Id == "room-2026-002");
        noPhotoRoom.ThumbnailUrl.Should().BeEmpty();
    }

    [Fact]
    public async Task Controller_Search_Returns200OkWithSearchResponseDto()
    {
        var controller = new ListingsController(
            new ListingService(_repo),
            new ListingDetailService(_repo),
            _sut);

        var action = await controller.Search(
            new ListingSearchQuery
            {
                CampusId = "ictu-main",
                MinRent = 1_500_000m,
                MaxRent = 2_200_000m,
                RoomType = "Khép kín",
                SortBy = "price"
            },
            CancellationToken.None);

        var ok = action.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var payload = ok.Value.Should().BeOfType<SearchResponseDto>().Subject;
        payload.TotalCount.Should().Be(2);
        payload.Items.Select(i => i.Id).Should().ContainInOrder("room-2026-004", "room-2026-005");
        payload.ActiveFilters.RoomType.Should().Be("Khép kín");
    }
}
