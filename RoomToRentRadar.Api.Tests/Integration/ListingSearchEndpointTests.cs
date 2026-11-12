using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

/// <summary>
/// The search filters of <c>GET /api/v1/listings</c>. The fixture is
/// <see cref="TestListingsSeed"/>: published rows 0-19 at
/// <c>(10.7 + i * 0.001, 106.7 + i * 0.001)</c>, so row <c>i</c> is about
/// <c>0.157 * i</c> km from the campus used below.
/// </summary>
[TestFixture]
public sealed class ListingSearchEndpointTests
{
    private const string Endpoint = "/api/v1/listings";
    private const double CampusLat = 10.7;
    private const double CampusLng = 106.7;

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

    [Test]
    public async Task AllFiltersTogether_ReturnOnlyMatchingPublishedListings()
    {
        // SharedRoom rows are indices 1, 6, 11 and 16; the price, area and distance
        // filters drop index 16 (5,000,000 VND).
        var result = await GetResultAsync(
            $"{Endpoint}?limit=100&min_price=6000000&max_price=8000000"
            + "&min_area_m2=35&max_area_m2=45&room_type=shared_room"
            + $"&campus_lat={Format(CampusLat)}&campus_lng={Format(CampusLng)}&max_distance_km=2");

        var expectedIds = new[]
        {
            TestListingsSeed.IdFor(1),
            TestListingsSeed.IdFor(6),
            TestListingsSeed.IdFor(11)
        };

        Assert.Multiple(() =>
        {
            Assert.That(result.Data.Select(x => x.Id), Is.EquivalentTo(expectedIds));
            Assert.That(result.TotalCount, Is.EqualTo(expectedIds.Length));
            Assert.That(result.TotalCountCapped, Is.False);
            Assert.That(
                result.Data,
                Has.All.Matches<ListingSummaryDto>(item =>
                    item.Price >= 6_000_000
                    && item.Price <= 8_000_000
                    && item.AreaM2 >= 35
                    && item.AreaM2 <= 45
                    && GeoDistance.HaversineKm(CampusLat, CampusLng, item.Lat, item.Lng) <= 2d));
        });
    }

    [Test]
    public async Task AreaRange_IsInclusiveAtBothBounds()
    {
        var result = await GetResultAsync($"{Endpoint}?limit=100&min_area_m2=35&max_area_m2=45");

        Assert.Multiple(() =>
        {
            Assert.That(result.Data, Has.Count.EqualTo(12));
            Assert.That(result.TotalCount, Is.EqualTo(12));
            Assert.That(result.Data, Has.All.Matches<ListingSummaryDto>(item => item.AreaM2 is >= 35 and <= 45));
            Assert.That(result.Data.Select(x => x.AreaM2), Does.Contain(35d), "the lower bound is included");
            Assert.That(result.Data.Select(x => x.AreaM2), Does.Contain(45d), "the upper bound is included");
        });
    }

    [Test]
    public async Task AreaRange_WithEqualBounds_ReturnsOnlyThatArea()
    {
        var result = await GetResultAsync($"{Endpoint}?limit=100&min_area_m2=50&max_area_m2=50");

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalCount, Is.EqualTo(4));
            Assert.That(result.Data, Has.All.Matches<ListingSummaryDto>(item => item.AreaM2 == 50d));
        });
    }

    [Test]
    public async Task RoomType_ReturnsOnlyThatRoomType()
    {
        var result = await GetResultAsync($"{Endpoint}?limit=100&room_type=studio");

        var expectedIds = new[]
        {
            TestListingsSeed.IdFor(2),
            TestListingsSeed.IdFor(7),
            TestListingsSeed.IdFor(12),
            TestListingsSeed.IdFor(17)
        };

        Assert.Multiple(() =>
        {
            Assert.That(result.Data.Select(x => x.Id), Is.EquivalentTo(expectedIds));
            Assert.That(result.TotalCount, Is.EqualTo(expectedIds.Length));
            Assert.That(result.ActiveFilters!.RoomType, Is.EqualTo(RoomType.Studio));
        });
    }

    [Test]
    public async Task DistanceFilter_ReturnsListingsInsideTheRadius()
    {
        var result = await GetResultAsync(CampusUrl(radiusKm: 1d));

        var expectedTitles = new[] { 0, 1, 2, 3, 4, 5, 6 }.Select(TitleFor);

        Assert.Multiple(() =>
        {
            Assert.That(result.Data.Select(x => x.Title), Is.EquivalentTo(expectedTitles));
            Assert.That(result.TotalCount, Is.EqualTo(7));
            Assert.That(
                result.Data,
                Has.All.Matches<ListingSummaryDto>(item =>
                    GeoDistance.HaversineKm(CampusLat, CampusLng, item.Lat, item.Lng) <= 1d));
        });
    }

    [Test]
    public async Task DistanceFilter_IncludesTheBoundaryAndExcludesTheNextListing()
    {
        var boundaryDistanceKm = SeedDistanceKm(3);

        var onBoundary = await GetResultAsync(CampusUrl(boundaryDistanceKm));
        var justInside = await GetResultAsync(CampusUrl(Math.BitDecrement(boundaryDistanceKm)));

        Assert.Multiple(() =>
        {
            Assert.That(
                onBoundary.Data.Select(x => x.Title),
                Does.Contain(TitleFor(3)),
                "a listing exactly on the radius is included");
            Assert.That(
                onBoundary.Data.Select(x => x.Title),
                Does.Not.Contain(TitleFor(4)),
                "the next listing is farther than the radius");
            Assert.That(onBoundary.TotalCount, Is.EqualTo(4));

            Assert.That(
                justInside.Data.Select(x => x.Title),
                Does.Not.Contain(TitleFor(3)),
                "a marginally smaller radius excludes the boundary");
            Assert.That(
                justInside.Data.Select(x => x.Title),
                Does.Contain(TitleFor(2)));
            Assert.That(justInside.TotalCount, Is.EqualTo(3));
        });
    }

    [TestCase(ListingSort.Newest)]
    [TestCase(ListingSort.PriceAsc)]
    public async Task DistanceFilter_PageWalk_ReturnsEveryMatchExactlyOnce(ListingSort sort)
    {
        var expectedTitles = new[] { 0, 1, 2, 3, 4, 5, 6 }.Select(TitleFor).ToList();

        var items = new List<ListingSummaryDto>();
        string? cursor = null;
        var pageCount = 0;
        var totals = new List<int>();

        do
        {
            var url = CampusUrl(radiusKm: 1d, limit: 2, sort: ListingSortWire.ToWireValue(sort));
            if (cursor is not null)
            {
                url += $"&cursor={Uri.EscapeDataString(cursor)}";
            }

            var page = await GetResultAsync(url);
            Assert.That(page.Data.Count, Is.LessThanOrEqualTo(2));

            items.AddRange(page.Data);
            totals.Add(page.TotalCount);
            cursor = page.Pagination.NextCursor;
            pageCount++;
        }
        while (cursor is not null && pageCount < 50);

        Assert.Multiple(() =>
        {
            Assert.That(pageCount, Is.GreaterThan(1));
            Assert.That(items, Has.Count.EqualTo(expectedTitles.Count));
            Assert.That(items.Select(x => x.Id).Distinct().Count(), Is.EqualTo(items.Count));
            Assert.That(items.Select(x => x.Title), Is.EquivalentTo(expectedTitles));
            Assert.That(totals, Has.All.EqualTo(expectedTitles.Count), "totalCount is stable across pages");
        });
    }

    [Test]
    public async Task NoFilters_ReturnsEveryPublishedListingWithItsTotalCount()
    {
        var result = await GetResultAsync($"{Endpoint}?limit=100");

        Assert.Multiple(() =>
        {
            Assert.That(result.Data, Has.Count.EqualTo(TestListingsSeed.PublishedCount));
            Assert.That(result.TotalCount, Is.EqualTo(TestListingsSeed.PublishedCount));
            Assert.That(result.TotalCountCapped, Is.False);
            Assert.That(result.ActiveFilters, Is.Null);
            Assert.That(result.Pagination.HasMore, Is.False);
        });
    }

    [Test]
    public async Task EmptyResult_ReportsZeroTotalAndTheActiveFilters()
    {
        var result = await GetResultAsync($"{Endpoint}?min_price=999000000");

        Assert.Multiple(() =>
        {
            Assert.That(result.Data, Is.Empty);
            Assert.That(result.TotalCount, Is.Zero);
            Assert.That(result.TotalCountCapped, Is.False);
            Assert.That(result.ActiveFilters, Is.EqualTo(new ActiveListingFiltersDto(
                999_000_000,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null)));
        });
    }

    [Test]
    public async Task TotalCount_IsCappedWhenMatchesExceedTheLimit()
    {
        const int seeded = 1005;

        await _factory.ResetDataAsync();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await TestListingsSeed.SeedBulkAsync(dbContext, seeded);
        }

        _factory.QueryCounter.Reset();

        var result = await GetResultAsync($"{Endpoint}?limit=50");
        var again = await GetResultAsync($"{Endpoint}?limit=50");

        Assert.Multiple(() =>
        {
            Assert.That(result.Data, Has.Count.EqualTo(50));
            Assert.That(result.TotalCount, Is.EqualTo(ListingSearchResultDto.TotalCountLimit));
            Assert.That(result.TotalCountCapped, Is.True);
            Assert.That(again.TotalCount, Is.EqualTo(result.TotalCount));
            Assert.That(again.TotalCountCapped, Is.True);
        });
    }

    [Test]
    public async Task ActiveFilters_EchoesOnlyTheProvidedFilters()
    {
        using var document = await GetJsonAsync(
            $"{Endpoint}?min_price=5000000&room_type=house&campus_lat={Format(CampusLat)}"
            + $"&campus_lng={Format(CampusLng)}&max_distance_km=2");
        var activeFilters = document.RootElement.GetProperty("activeFilters");

        Assert.Multiple(() =>
        {
            Assert.That(activeFilters.GetProperty("minPrice").GetInt64(), Is.EqualTo(5_000_000));
            Assert.That(activeFilters.GetProperty("roomType").GetString(), Is.EqualTo(RoomTypeWire.House));
            Assert.That(activeFilters.GetProperty("campusLat").GetDouble(), Is.EqualTo(CampusLat));
            Assert.That(activeFilters.GetProperty("campusLng").GetDouble(), Is.EqualTo(CampusLng));
            Assert.That(activeFilters.GetProperty("maxDistanceKm").GetDouble(), Is.EqualTo(2d));

            Assert.That(activeFilters.TryGetProperty("maxPrice", out _), Is.False);
            Assert.That(activeFilters.TryGetProperty("minAreaM2", out _), Is.False);
            Assert.That(activeFilters.TryGetProperty("maxAreaM2", out _), Is.False);
            Assert.That(activeFilters.TryGetProperty("bedrooms", out _), Is.False);
        });
    }

    [Test]
    public async Task ActiveFilters_IsNullWhenNoFiltersAreProvided()
    {
        using var document = await GetJsonAsync(Endpoint);

        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.TryGetProperty("activeFilters", out var activeFilters), Is.True);
            Assert.That(activeFilters.ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(document.RootElement.GetProperty("totalCount").GetInt32(), Is.EqualTo(TestListingsSeed.PublishedCount));
        });
    }

    [TestCase("min_area_m2=-1", ListingProblemFactory.InvalidAreaRange)]
    [TestCase("min_area_m2=NaN", ListingProblemFactory.InvalidAreaRange)]
    [TestCase("min_area_m2=50&max_area_m2=40", ListingProblemFactory.InvalidAreaRange)]
    [TestCase("room_type=castle", ListingProblemFactory.InvalidRoomType)]
    [TestCase("room_type=Studio", ListingProblemFactory.InvalidRoomType)]
    [TestCase("campus_lat=10.7", ListingProblemFactory.InvalidCampusFilter)]
    [TestCase("campus_lat=10.7&campus_lng=106.7", ListingProblemFactory.InvalidCampusFilter)]
    [TestCase("max_distance_km=5", ListingProblemFactory.InvalidCampusFilter)]
    [TestCase("campus_lat=91&campus_lng=106.7&max_distance_km=5", ListingProblemFactory.InvalidCampusCoordinates)]
    [TestCase("campus_lat=10.7&campus_lng=181&max_distance_km=5", ListingProblemFactory.InvalidCampusCoordinates)]
    [TestCase("campus_lat=10.7&campus_lng=106.7&max_distance_km=0", ListingProblemFactory.InvalidDistanceRange)]
    [TestCase("campus_lat=10.7&campus_lng=106.7&max_distance_km=50.5", ListingProblemFactory.InvalidDistanceRange)]
    public async Task InvalidFilter_ReturnsProblemDetails(string queryString, string expectedCode)
    {
        var problem = await GetProblemAsync($"{Endpoint}?{queryString}");

        Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo(expectedCode));
    }

    [Test]
    public async Task SqlPath_IssuesOneCountAndOnePageQuery()
    {
        _factory.QueryCounter.Reset();

        var result = await GetResultAsync($"{Endpoint}?limit=10&room_type=house");

        Assert.Multiple(() =>
        {
            Assert.That(result.Data, Is.Not.Empty);
            Assert.That(_factory.QueryCounter.ListingsSelectCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task DistancePath_IssuesOneCandidateAndOnePhotoQuery()
    {
        _factory.QueryCounter.Reset();

        var result = await GetResultAsync(CampusUrl(radiusKm: 1d));

        Assert.Multiple(() =>
        {
            Assert.That(result.Data, Is.Not.Empty);
            Assert.That(_factory.QueryCounter.ListingsSelectCount, Is.EqualTo(1));
            Assert.That(_factory.QueryCounter.ListingPhotoSelectCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task DistancePath_EmptyPage_DoesNotQueryPhotos()
    {
        _factory.QueryCounter.Reset();

        // 1 km around (0, 0) is far away from every seeded listing.
        var result = await GetResultAsync(
            $"{Endpoint}?campus_lat=0&campus_lng=0&max_distance_km=1");

        Assert.Multiple(() =>
        {
            Assert.That(result.Data, Is.Empty);
            Assert.That(result.TotalCount, Is.Zero);
            Assert.That(_factory.QueryCounter.ListingsSelectCount, Is.EqualTo(1));
            Assert.That(_factory.QueryCounter.ListingPhotoSelectCount, Is.Zero);
        });
    }

    private static string CampusUrl(double radiusKm, int? limit = null, string? sort = null)
    {
        var url = $"{Endpoint}?campus_lat={Format(CampusLat)}&campus_lng={Format(CampusLng)}"
            + $"&max_distance_km={Format(radiusKm)}";

        if (limit is { } limitValue)
        {
            url += $"&limit={limitValue}";
        }

        if (sort is not null)
        {
            url += $"&sort={sort}";
        }

        return url;
    }

    // The query string must round-trip exactly, so culture-independent "R" formatting
    // is used for every double.
    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string TitleFor(int index) => $"Test listing {index}";

    private static double SeedDistanceKm(int index)
        => GeoDistance.HaversineKm(
            CampusLat,
            CampusLng,
            10.7 + index * 0.001,
            106.7 + index * 0.001);

    private async Task<ListingSearchResultDto> GetResultAsync(string url)
    {
        var response = await _client.GetAsync(url);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), payload);

        var result = JsonSerializer.Deserialize<ListingSearchResultDto>(payload, ApiJson.Options);
        Assert.That(result, Is.Not.Null, payload);
        return result!;
    }

    private async Task<JsonDocument> GetJsonAsync(string url)
    {
        var response = await _client.GetAsync(url);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), payload);
        return JsonDocument.Parse(payload);
    }

    private async Task<JsonElement> GetProblemAsync(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), url);
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        });

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
