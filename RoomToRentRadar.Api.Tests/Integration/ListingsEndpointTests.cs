using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class ListingsEndpointTests
{
    private ListingsApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new ListingsApiFactory();
        await _factory.InitializeDatabaseAsync();
        _client = _factory.CreateClient();
    }

    [SetUp]
    public async Task SetUp()
    {
        await _factory.ResetDataAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await TestListingsSeed.SeedAsync(dbContext);
        _factory.QueryCounter.Reset();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [TestCase(ListingSort.Newest)]
    [TestCase(ListingSort.PriceAsc)]
    [TestCase(ListingSort.PriceDesc)]
    [TestCase(ListingSort.AreaDesc)]
    public async Task PageWalk_ReturnsEveryPublishedListingExactlyOnce(ListingSort sort)
    {
        var allItems = new List<ListingSummaryDto>();
        string? cursor = null;
        var pageCount = 0;

        do
        {
            var url = $"/api/v1/listings?limit=3&sort={ListingSortWire.ToWireValue(sort)}";
            if (cursor is not null)
            {
                url += $"&cursor={Uri.EscapeDataString(cursor)}";
            }

            var page = await GetPageAsync(url);
            Assert.That(page.Data.Count, Is.LessThanOrEqualTo(3));
            Assert.That(page.Pagination.Limit, Is.EqualTo(3));

            allItems.AddRange(page.Data);
            cursor = page.Pagination.NextCursor;

            if (!page.Pagination.HasMore)
            {
                Assert.That(cursor, Is.Null);
            }

            pageCount++;
        }
        while (cursor is not null && pageCount < 50);

        Assert.That(allItems, Has.Count.EqualTo(TestListingsSeed.PublishedCount));
        Assert.That(allItems.Select(x => x.Id).Distinct().Count(), Is.EqualTo(allItems.Count));
        AssertSorted(allItems, sort);

        var expectedIds = await QueryPublishedIdsAsync();
        Assert.That(allItems.Select(x => x.Id), Is.EquivalentTo(expectedIds));
    }

    [Test]
    public async Task DefaultQuery_UsesDefaultLimitAndReturnsAllPublishedRows()
    {
        var page = await GetPageAsync("/api/v1/listings");

        Assert.Multiple(() =>
        {
            Assert.That(page.Pagination.Limit, Is.EqualTo(20));
            Assert.That(page.Data, Has.Count.EqualTo(20));
            Assert.That(page.Pagination.HasMore, Is.False);
            Assert.That(page.Pagination.NextCursor, Is.Null);
        });
    }

    [Test]
    public async Task FiltersAndSort_AreAppliedTogether()
    {
        var page = await GetPageAsync(
            "/api/v1/listings?limit=100&min_price=6000000&max_price=7000000&bedrooms=2&sort=price_desc");

        Assert.That(page.Data, Is.Not.Empty);
        Assert.That(
            page.Data,
            Has.All.Matches<ListingSummaryDto>(item =>
                item.Price >= 6_000_000
                && item.Price <= 7_000_000
                && item.Bedrooms == 2));

        AssertSorted(page.Data, ListingSort.PriceDesc);

        var expectedIds = await QueryIdsAsync(dbContext => dbContext.Listings
            .Where(x => x.Status == ListingStatus.Published
                && x.Price >= 6_000_000
                && x.Price <= 7_000_000
                && x.Bedrooms == 2)
            .Select(x => x.Id));

        Assert.That(page.Data.Select(x => x.Id), Is.EquivalentTo(expectedIds));
    }

    [Test]
    public async Task EmptyResult_ReturnsEmptyPage()
    {
        var page = await GetPageAsync("/api/v1/listings?min_price=999000000");

        Assert.Multiple(() =>
        {
            Assert.That(page.Data, Is.Empty);
            Assert.That(page.Pagination.HasMore, Is.False);
            Assert.That(page.Pagination.NextCursor, Is.Null);
        });
    }

    [Test]
    public async Task NonPublishedListings_AreNeverReturned()
    {
        var page = await GetPageAsync("/api/v1/listings?limit=100");

        var returnedIds = page.Data.Select(x => x.Id).ToHashSet();
        var hiddenIds = await QueryIdsAsync(dbContext => dbContext.Listings
            .Where(x => x.Status != ListingStatus.Published)
            .Select(x => x.Id));

        Assert.Multiple(() =>
        {
            Assert.That(returnedIds, Has.Count.EqualTo(TestListingsSeed.PublishedCount));
            Assert.That(returnedIds.Overlaps(hiddenIds), Is.False);
        });
    }

    [Test]
    public async Task CoverPhoto_IsIncludedByTheCountAndPageQueriesOfTheSqlPath()
    {
        _factory.QueryCounter.Reset();

        var page = await GetPageAsync("/api/v1/listings?limit=100");
        var byTitle = page.Data.ToDictionary(x => x.Title);

        Assert.Multiple(() =>
        {
            Assert.That(byTitle["Test listing 0"].CoverPhotoUrl, Is.EqualTo("/media/test/thumbs/cover-special.jpg"));
            Assert.That(byTitle["Test listing 1"].CoverPhotoUrl, Is.Null);
            Assert.That(byTitle["Test listing 2"].CoverPhotoUrl, Is.EqualTo("/media/test/thumbs/photo-1.jpg"));
            Assert.That(byTitle["Test listing 3"].CoverPhotoUrl, Is.EqualTo("/media/test/thumbs/cover-3.jpg"));
            Assert.That(
                _factory.QueryCounter.ListingsSelectCount,
                Is.EqualTo(2),
                "the total count query and the page query");
        });
    }

    [Test]
    public async Task CursorSortMismatch_IsRejected()
    {
        var firstPage = await GetPageAsync("/api/v1/listings?limit=2&sort=price_asc");
        Assert.That(firstPage.Pagination.NextCursor, Is.Not.Null);

        var problem = await GetProblemAsync(
            $"/api/v1/listings?limit=2&sort=newest&cursor={Uri.EscapeDataString(firstPage.Pagination.NextCursor!)}");

        Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo("cursor_sort_mismatch"));
    }

    [Test]
    public async Task CursorFilterMismatch_IsRejected()
    {
        var firstPage = await GetPageAsync("/api/v1/listings?limit=2&min_price=6000000");
        Assert.That(firstPage.Pagination.NextCursor, Is.Not.Null);

        var problem = await GetProblemAsync(
            "/api/v1/listings?limit=2&min_price=7000000&cursor="
            + Uri.EscapeDataString(firstPage.Pagination.NextCursor!));

        Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo("cursor_filter_mismatch"));
    }

    [Test]
    public async Task Cursor_IsAcceptedWhenFiltersAreUnchanged()
    {
        var firstPage = await GetPageAsync("/api/v1/listings?limit=2&min_price=6000000");
        Assert.That(firstPage.Pagination.NextCursor, Is.Not.Null);

        var secondPage = await GetPageAsync(
            "/api/v1/listings?limit=2&min_price=6000000&cursor="
            + Uri.EscapeDataString(firstPage.Pagination.NextCursor!));

        Assert.Multiple(() =>
        {
            Assert.That(secondPage.Data, Has.Count.EqualTo(2));
            Assert.That(
                secondPage.Data.Select(item => item.Id)
                    .Intersect(firstPage.Data.Select(item => item.Id)),
                Is.Empty);
        });
    }

    [TestCase("limit=0", "invalid_limit")]
    [TestCase("limit=101", "invalid_limit")]
    [TestCase("limit=abc", "invalid_parameter_format")]
    [TestCase("sort=popular", "invalid_sort")]
    [TestCase("min_price=-1", "invalid_price_range")]
    [TestCase("min_price=10&max_price=5", "invalid_price_range")]
    [TestCase("bedrooms=21", "invalid_bedrooms")]
    [TestCase("cursor=not-a-cursor", "invalid_cursor")]
    public async Task InvalidQuery_ReturnsProblemDetails(string queryString, string expectedCode)
    {
        var problem = await GetProblemAsync($"/api/v1/listings?{queryString}");

        Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo(expectedCode));
    }

    [Test]
    public async Task GetListing_PublishedListing_ReturnsDetail()
    {
        // Arrange
        // EF Core replaces the default key of the first seeded listing (IdFor(0) is Guid.Empty)
        // with a generated value, so the published listing is resolved by its unique title.
        var id = await QueryListingIdByTitleAsync("Test listing 0");

        // Act
        var response = await _client.GetAsync($"/api/v1/listings/{id}");
        var detail = await response.Content.ReadFromJsonAsync<ListingDetailDto>(ApiJson.Options);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
            Assert.That(detail, Is.Not.Null);
            Assert.That(detail!.Id, Is.EqualTo(id));
            Assert.That(detail.Title, Is.EqualTo("Test listing 0"));
            Assert.That(detail.Description, Is.EqualTo("Test listing 0 description"));
            Assert.That(detail.Price, Is.EqualTo(5_000_000));
            Assert.That(detail.AreaM2, Is.EqualTo(30));
            Assert.That(detail.Bedrooms, Is.EqualTo(0));
            Assert.That(detail.Address, Is.EqualTo("Test address 0, District 0"));
            Assert.That(detail.CreatedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(detail.UpdatedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(detail.CreatedAt, Is.EqualTo(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(detail.UpdatedAt, Is.EqualTo(new DateTime(2026, 1, 1, 0, 5, 0, DateTimeKind.Utc)));
        });
    }

    [TestCase(20)]
    [TestCase(22)]
    [TestCase(24)]
    public async Task GetListing_NonPublishedListing_ReturnsGone(int index)
    {
        // Arrange
        var id = TestListingsSeed.IdFor(index);

        // Act
        var problem = await GetProblemAsync($"/api/v1/listings/{id}", HttpStatusCode.Gone);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo("listing_unavailable"));
            Assert.That(problem.GetRawText(), Does.Not.Contain($"Test listing {index}"));
        });
    }

    [Test]
    public async Task GetListing_UnknownListing_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var problem = await GetProblemAsync($"/api/v1/listings/{id}", HttpStatusCode.NotFound);

        // Assert
        Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo("listing_not_found"));
    }

    private async Task<ListingSearchResultDto> GetPageAsync(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), url);
        var result = await response.Content.ReadFromJsonAsync<ListingSearchResultDto>(ApiJson.Options);
        Assert.That(result, Is.Not.Null);
        return result!;
    }

    private async Task<JsonElement> GetProblemAsync(
        string url,
        HttpStatusCode expectedStatus = HttpStatusCode.BadRequest)
    {
        var response = await _client.GetAsync(url);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(expectedStatus), url);
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        });

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private async Task<List<Guid>> QueryPublishedIdsAsync()
        => await QueryIdsAsync(dbContext => dbContext.Listings
            .Where(x => x.Status == ListingStatus.Published)
            .Select(x => x.Id));

    private async Task<Guid> QueryListingIdByTitleAsync(string title)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.Listings
            .Where(x => x.Title == title)
            .Select(x => x.Id)
            .SingleAsync();
    }

    private async Task<List<Guid>> QueryIdsAsync(Func<AppDbContext, IQueryable<Guid>> query)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await query(dbContext).ToListAsync();
    }

    private static void AssertSorted(IReadOnlyList<ListingSummaryDto> items, ListingSort sort)
    {
        for (var index = 1; index < items.Count; index++)
        {
            var previous = items[index - 1];
            var current = items[index];
            var comparison = sort switch
            {
                ListingSort.Newest => -previous.CreatedAt.CompareTo(current.CreatedAt),
                ListingSort.PriceAsc => previous.Price.CompareTo(current.Price),
                ListingSort.PriceDesc => -previous.Price.CompareTo(current.Price),
                ListingSort.AreaDesc => -previous.AreaM2.CompareTo(current.AreaM2),
                _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, null)
            };

            Assert.That(comparison, Is.LessThanOrEqualTo(0), $"Items are not sorted by {sort} at index {index}.");

            if (comparison == 0)
            {
                Assert.That(
                    previous.Id.CompareTo(current.Id),
                    Is.LessThan(0),
                    $"Equal sort keys must be ordered by id at index {index}.");
            }
        }
    }
}
