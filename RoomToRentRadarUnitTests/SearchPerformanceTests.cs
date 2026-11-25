using System.Diagnostics;
using FluentAssertions;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services;
using RoomToRentRadar.Shared.Dtos;
using RoomToRentRadarUnitTests.TestData;
using Xunit;

namespace RoomToRentRadarUnitTests;

public class SearchPerformanceTests
{
    private static readonly string[] RoomTypes = ["Studio", "Khép kín", "Chung cư mini", "Ở ghép"];
    private static readonly string[] SortOptions = ["distance", "price", "price_desc", "area"];

    [Fact]
    public async Task Search_500Listings_CombinedFilters_CompletesWithin2Seconds()
    {
        var repo = new InMemoryListingRepository(SearchListingTestDataFactory.CreateLargeDataset(500));
        var sut = new ListingSearchService(repo);
        var query = new ListingSearchQuery
        {
            CampusId = "ictu-main",
            MinRent = 1_200_000m,
            MaxRent = 2_800_000m,
            MinArea = 18,
            MaxArea = 35,
            RoomType = "Studio",
            MaxDistanceMeters = 1800,
            SortBy = "price",
            PageIndex = 1,
            PageSize = 20
        };

        await sut.SearchAsync(query);

        var sw = Stopwatch.StartNew();
        var result = await sut.SearchAsync(query);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(2000);
        result.TotalCount.Should().BeGreaterThan(0);
        result.Items.Should().OnlyContain(i =>
            i.Status == "Available"
            && i.IsVerified
            && i.CampusId == "ictu-main"
            && i.Rent >= 1_200_000m
            && i.Rent <= 2_800_000m
            && i.Area >= 18
            && i.Area <= 35
            && i.RoomType == "Studio"
            && i.DistanceMeters <= 1800);
        result.Items.Should().BeInAscendingOrder(i => i.Rent);
    }

    [Fact]
    public async Task Search_500Listings_FullPaginationTraversal_CompletesWithin3Seconds()
    {
        var repo = new InMemoryListingRepository(SearchListingTestDataFactory.CreateLargeDataset(500));
        var sut = new ListingSearchService(repo);

        await sut.SearchAsync(new ListingSearchQuery { PageIndex = 1, PageSize = 50 });

        var sw = Stopwatch.StartNew();
        var collectedIds = new List<string>();
        int expectedTotal = 0;
        int page = 1;

        while (true)
        {
            var res = await sut.SearchAsync(new ListingSearchQuery
            {
                CampusId = "ictu-main",
                PageIndex = page,
                PageSize = 50,
                SortBy = "distance"
            });

            expectedTotal = res.TotalCount;
            collectedIds.AddRange(res.Items.Select(i => i.Id));

            if (!res.HasNextPage)
            {
                break;
            }

            page++;
        }

        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(3000);
        expectedTotal.Should().BeGreaterThan(300);
        collectedIds.Should().HaveCount(expectedTotal);
        collectedIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Search_2000Listings_50CombinedFilterQueries_P95Under2Seconds()
    {
        var repo = new InMemoryListingRepository(SearchListingTestDataFactory.CreateLargeDataset(2000));
        var sut = new ListingSearchService(repo);

        await sut.SearchAsync(new ListingSearchQuery());

        var durationsMs = new List<double>(50);

        for (int i = 0; i < 50; i++)
        {
            var query = new ListingSearchQuery
            {
                CampusId = i % 5 == 0 ? "ictu-cs2" : "ictu-main",
                MinRent = 1_000_000m + (i % 5) * 200_000m,
                MaxRent = 3_200_000m + (i % 4) * 300_000m,
                MinArea = 15 + (i % 4) * 2,
                MaxArea = 35 + (i % 3) * 3,
                RoomType = RoomTypes[i % RoomTypes.Length],
                MaxDistanceMeters = 1200 + (i % 5) * 400,
                SortBy = SortOptions[i % SortOptions.Length],
                PageIndex = 1,
                PageSize = 20
            };

            var sw = Stopwatch.StartNew();
            var res = await sut.SearchAsync(query);
            sw.Stop();

            durationsMs.Add(sw.Elapsed.TotalMilliseconds);

            res.Items.Should().OnlyContain(item =>
                item.Status == "Available"
                && item.IsVerified
                && item.CampusId == query.CampusId
                && item.Rent >= query.MinRent
                && item.Rent <= query.MaxRent
                && item.Area >= query.MinArea
                && item.Area <= query.MaxArea
                && item.RoomType == query.RoomType
                && item.DistanceMeters <= query.MaxDistanceMeters);
        }

        durationsMs.Sort();
        double p95Ms = durationsMs[(int)Math.Ceiling(durationsMs.Count * 0.95) - 1];
        double maxMs = durationsMs[^1];

        p95Ms.Should().BeLessThan(2000);
        maxMs.Should().BeLessThan(2000);
    }

    [Fact]
    public async Task Search_500Listings_ConcurrentQueries_ThreadSafeAndUnder2Seconds()
    {
        var repo = new InMemoryListingRepository(SearchListingTestDataFactory.CreateLargeDataset(500));
        var sut = new ListingSearchService(repo);

        await sut.SearchAsync(new ListingSearchQuery());

        var sw = Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, 20).Select(i => sut.SearchAsync(new ListingSearchQuery
        {
            CampusId = "ictu-main",
            MinRent = 1_200_000m,
            MaxRent = 3_000_000m,
            MinArea = 16,
            MaxArea = 36,
            RoomType = RoomTypes[i % RoomTypes.Length],
            MaxDistanceMeters = 2000,
            SortBy = SortOptions[i % SortOptions.Length],
            PageIndex = (i % 2) + 1,
            PageSize = 15
        }));

        var results = await Task.WhenAll(tasks);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(2000);
        results.Should().OnlyContain(r => r.Items.All(item => item.Status == "Available" && item.IsVerified));
    }
}
