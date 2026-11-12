using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Controllers;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services;
using RoomToRentRadar.Shared.Dtos;
using Xunit;

namespace RoomToRentRadarUnitTests;

public class ListingPaginationServiceTests
{
    private readonly InMemoryListingRepository _repo = new();
    private readonly ListingService _sut;

    public ListingPaginationServiceTests()
    {
        _sut = new ListingService(_repo);
    }

    [Fact]
    public void Request_HasExpectedDefaults()
    {
        var request = new ListingQueryRequest();
        request.PageNumber.Should().Be(1);
        request.PageSize.Should().Be(10);
        request.SortBy.Should().Be("distance");
        request.IsAscending.Should().BeTrue();
    }

    [Xunit.Theory]
    [InlineData(0, 10, 0)]
    [InlineData(25, 10, 3)]
    [InlineData(20, 10, 2)]
    [InlineData(1, 10, 1)]
    public void PagedResult_ComputesTotalPagesAndMetadata(int totalRecords, int pageSize, int expectedPages)
    {
        var result = PagedResult<ListingCardDto>.Create([], totalRecords, 1, pageSize);
        result.TotalRecords.Should().Be(totalRecords);
        result.TotalCount.Should().Be(totalRecords);
        result.TotalPages.Should().Be(expectedPages);
        result.CurrentPage.Should().Be(1);
        result.PageIndex.Should().Be(1);
    }

    [Fact]
    public async Task GetListings_PaginatesAndSortsByRentAscendingAndDescending()
    {
        var asc = await _sut.GetListingsAsync(new ListingQueryRequest
        {
            CampusId = "ictu-main",
            PageNumber = 1,
            PageSize = 2,
            SortBy = "rent",
            IsAscending = true
        });

        asc.Items.Should().HaveCount(2);
        asc.Items.Should().OnlyContain(i => i.Status == "Available" && i.IsVerified);
        asc.Items.Select(i => i.Rent).Should().BeInAscendingOrder();
        asc.TotalRecords.Should().BeGreaterThan(2);
        asc.TotalPages.Should().Be((int)Math.Ceiling(asc.TotalRecords / 2.0));
        asc.CurrentPage.Should().Be(1);
        asc.HasNextPage.Should().BeTrue();

        var desc = await _sut.GetListingsAsync(new ListingQueryRequest
        {
            CampusId = "ictu-main",
            PageNumber = 1,
            PageSize = 2,
            SortBy = "rent",
            IsAscending = false
        });

        desc.Items.Select(i => i.Rent).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Controller_GetListings_Returns200OkWithPagedResult()
    {
        var controller = new ListingsController(
            _sut,
            new ListingDetailService(_repo),
            new ListingSearchService(_repo));

        var action = await controller.GetListings(
            new ListingQueryRequest { PageNumber = 1, PageSize = 5, SortBy = "area", IsAscending = false },
            CancellationToken.None);

        var ok = action.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var payload = ok.Value.Should().BeOfType<PagedResult<ListingCardDto>>().Subject;
        payload.CurrentPage.Should().Be(1);
        payload.PageSize.Should().Be(5);
        payload.TotalRecords.Should().BeGreaterThan(0);
        payload.Items.Select(i => i.Area).Should().BeInDescendingOrder();
    }
}
