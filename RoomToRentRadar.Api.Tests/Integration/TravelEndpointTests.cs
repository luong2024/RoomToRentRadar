using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Api.Features.Travel;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Serialization;
using RoomToRentRadar.Shared.Travel;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class TravelEndpointTests
{
    // Index 0 of TestListingsSeed is Guid.Empty, which EF treats as an unset key
    // and replaces on insert, so published-listing tests use index 3.
    private static readonly Guid PublishedListingId = TestListingsSeed.IdFor(3);
    private const double PublishedLat = 10.703;
    private const double PublishedLng = 106.703;

    private readonly FakeRoutingEstimatesProvider _provider = new();
    private ListingsApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new ListingsApiFactory(services =>
        {
            services.RemoveAll<IRoutingEstimatesProvider>();
            services.AddSingleton<IRoutingEstimatesProvider>(_provider);
        });
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
        (_factory.Services.GetRequiredService<IMemoryCache>() as MemoryCache)?.Clear();
        _provider.Reset();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task GetTravel_PublishedListing_ReturnsRouteEstimatesForThreeModes()
    {
        _provider.Handler = (_, _, _, _, mode) => mode switch
        {
            TravelMode.Motorbike => new RoutingEstimate(6200, 1080),
            TravelMode.Bus => new RoutingEstimate(7000, 1800),
            _ => new RoutingEstimate(5000, 3600)
        };

        var response = await GetTravelAsync(PublishedListingId, 10.7801, 106.7191);
        var payload = await ReadPayloadAsync(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(
                payload.Modes.Select(x => x.Mode),
                Is.EqualTo(new[] { TravelMode.Motorbike, TravelMode.Bus, TravelMode.Walking }));
            Assert.That(
                payload.Modes.Select(x => x.Source),
                Is.EqualTo(new[] { TravelSource.Route, TravelSource.Route, TravelSource.Route }));
            Assert.That(payload.Modes[0].DistanceM, Is.EqualTo(6200));
            Assert.That(payload.Modes[0].DurationS, Is.EqualTo(1080));
            Assert.That(payload.Modes[1].DistanceM, Is.EqualTo(7000));
            Assert.That(payload.Modes[1].DurationS, Is.EqualTo(1800));
            Assert.That(payload.Modes[2].DistanceM, Is.EqualTo(5000));
            Assert.That(payload.Modes[2].DurationS, Is.EqualTo(3600));
            Assert.That(_provider.CallCount, Is.EqualTo(3));
            Assert.That(
                _provider.Calls.Select(x => x.Mode),
                Is.EquivalentTo(Enum.GetValues<TravelMode>()));
        });
    }

    [Test]
    public async Task GetTravel_RouteResponse_UsesSnakeCaseWireValues()
    {
        _provider.Handler = (_, _, _, _, _) => new RoutingEstimate(1000, 60);

        var response = await GetTravelAsync(PublishedListingId, 10.7812, 106.7202);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var firstMode = document.RootElement.GetProperty("modes")[0];

        Assert.Multiple(() =>
        {
            Assert.That(firstMode.GetProperty("mode").GetString(), Is.EqualTo("motorbike"));
            Assert.That(firstMode.GetProperty("source").GetString(), Is.EqualTo("route"));
            Assert.That(firstMode.GetProperty("distanceM").GetInt32(), Is.EqualTo(1000));
            Assert.That(firstMode.GetProperty("durationS").GetInt32(), Is.EqualTo(60));
        });
    }

    [Test]
    public async Task GetTravel_RepeatedRequestWithinCacheWindow_CallsProviderOnce()
    {
        _provider.Handler = (_, _, _, _, mode) => new RoutingEstimate(1000 + (int)mode, 60);
        var listingId = PublishedListingId;

        var first = await ReadPayloadAsync(await GetTravelAsync(listingId, 10.7823, 106.7213));
        var second = await ReadPayloadAsync(await GetTravelAsync(listingId, 10.7823, 106.7213));

        Assert.Multiple(() =>
        {
            Assert.That(_provider.CallCount, Is.EqualTo(3), "the second request must be served from the cache");
            Assert.That(
                second.Modes.Select(x => x.DistanceM),
                Is.EqualTo(first.Modes.Select(x => x.DistanceM)));
        });
    }

    [Test]
    public async Task GetTravel_ProviderHasNoBusRoute_FallsBackToStraightLineForBusOnly()
    {
        _provider.Handler = (_, _, _, _, mode) => mode == TravelMode.Bus
            ? null
            : new RoutingEstimate(4200, 900);

        var payload = await ReadPayloadAsync(await GetTravelAsync(
            PublishedListingId,
            10.7601,
            106.6901));
        var bus = payload.Modes.Single(x => x.Mode == TravelMode.Bus);
        var expectedDistanceM = (int)Math.Round(
            GeoDistance.HaversineKm(PublishedLat, PublishedLng, 10.7601, 106.6901) * 1000d,
            MidpointRounding.AwayFromZero);

        Assert.Multiple(() =>
        {
            Assert.That(bus.Source, Is.EqualTo(TravelSource.Fallback));
            Assert.That(bus.DurationS, Is.Null);
            Assert.That(bus.DistanceM, Is.EqualTo(expectedDistanceM));
            Assert.That(payload.Modes[0].Source, Is.EqualTo(TravelSource.Route));
            Assert.That(payload.Modes[2].Source, Is.EqualTo(TravelSource.Route));
        });
    }

    [Test]
    public async Task GetTravel_ProviderUnavailable_ReturnsFallbackForEveryMode()
    {
        _provider.Handler = (_, _, _, _, _) => null;

        var response = await GetTravelAsync(PublishedListingId, 10.7612, 106.6912);
        var payload = await ReadPayloadAsync(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(payload.Modes, Has.Count.EqualTo(3));
            Assert.That(payload.Modes.Select(x => x.Source), Is.All.EqualTo(TravelSource.Fallback));
            Assert.That(payload.Modes.Select(x => x.DurationS), Is.All.Null);
            Assert.That(payload.Modes.Select(x => x.DistanceM), Is.All.GreaterThan(0));
        });
    }

    [Test]
    public async Task GetTravel_ProviderUnavailable_RepeatedRequestWithinFallbackWindow_SkipsProvider()
    {
        _provider.Handler = (_, _, _, _, _) => null;
        const double toLat = 10.7634;
        const double toLng = 106.6934;

        var first = await ReadPayloadAsync(await GetTravelAsync(PublishedListingId, toLat, toLng));
        var second = await ReadPayloadAsync(await GetTravelAsync(PublishedListingId, toLat, toLng));

        Assert.Multiple(() =>
        {
            Assert.That(
                _provider.CallCount,
                Is.EqualTo(3),
                "the second request must be served from the short-lived fallback cache");
            Assert.That(
                second.Modes.Select(x => x.DistanceM),
                Is.EqualTo(first.Modes.Select(x => x.DistanceM)));
            Assert.That(second.Modes.Select(x => x.Source), Is.All.EqualTo(TravelSource.Fallback));
        });
    }

    [TestCase(20)]
    [TestCase(21)]
    [TestCase(22)]
    [TestCase(23)]
    [TestCase(24)]
    public async Task GetTravel_NonPublishedListing_ReturnsNotFound(int seedIndex)
    {
        var response = await GetTravelAsync(TestListingsSeed.IdFor(seedIndex), 10.7701, 106.7001);
        var code = await ReadProblemCodeAsync(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(code, Is.EqualTo(ListingProblemFactory.ListingNotFound));
        });
        Assert.That(_provider.CallCount, Is.Zero);
    }

    [Test]
    public async Task GetTravel_UnknownListing_ReturnsNotFound()
    {
        var response = await GetTravelAsync(Guid.NewGuid(), 10.7701, 106.7001);
        var code = await ReadProblemCodeAsync(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(code, Is.EqualTo(ListingProblemFactory.ListingNotFound));
        });
    }

    [Test]
    public async Task GetTravel_MissingDestination_ReturnsInvalidDestination()
    {
        var response = await _client.GetAsync($"/api/v1/listings/{PublishedListingId}/travel");
        var code = await ReadProblemCodeAsync(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(code, Is.EqualTo(TravelProblemFactory.InvalidDestination));
        });
        Assert.That(_provider.CallCount, Is.Zero);
    }

    [TestCase("NaN", "106.7")]
    [TestCase("Infinity", "106.7")]
    [TestCase("91", "106.7")]
    [TestCase("-90.1", "106.7")]
    [TestCase("10.7", "181")]
    [TestCase("10.7", "-180.1")]
    public async Task GetTravel_InvalidDestination_ReturnsInvalidDestination(string toLat, string toLng)
    {
        var response = await _client.GetAsync(
            $"/api/v1/listings/{PublishedListingId}/travel?to_lat={toLat}&to_lng={toLng}");
        var code = await ReadProblemCodeAsync(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(code, Is.EqualTo(TravelProblemFactory.InvalidDestination));
        });
        Assert.That(_provider.CallCount, Is.Zero);
    }

    [Test]
    public async Task GetTravel_NonNumericDestination_ReturnsInvalidParameterFormat()
    {
        var response = await _client.GetAsync(
            $"/api/v1/listings/{PublishedListingId}/travel?to_lat=abc&to_lng=106.7");
        var code = await ReadProblemCodeAsync(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(code, Is.EqualTo(ListingProblemFactory.InvalidParameterFormat));
        });
        Assert.That(_provider.CallCount, Is.Zero);
    }

    private Task<HttpResponseMessage> GetTravelAsync(Guid listingId, double toLat, double toLng)
        => _client.GetAsync(FormattableString.Invariant(
            $"/api/v1/listings/{listingId}/travel?to_lat={toLat}&to_lng={toLng}"));

    private static async Task<ListingTravelEstimatesDto> ReadPayloadAsync(HttpResponseMessage response)
    {
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<ListingTravelEstimatesDto>(ApiJson.Options);
        Assert.That(payload, Is.Not.Null);
        return payload!;
    }

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code)
            ? code.GetString()
            : null;
    }
}

[TestFixture]
public sealed class TravelEndpointFallbackWithoutKeyTests
{
    private static readonly Guid PublishedListingId = TestListingsSeed.IdFor(3);
    private const double PublishedLat = 10.703;
    private const double PublishedLng = 106.703;

    private ListingsApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Keep the real provider but guarantee it has no key, so every call falls back.
        _factory = new ListingsApiFactory(services =>
            services.Configure<TravelOptions>(options => options.GoogleRoutesApiKey = null));
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
        (_factory.Services.GetRequiredService<IMemoryCache>() as MemoryCache)?.Clear();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task GetTravel_NoRoutingKeyConfigured_AlwaysReturnsStraightLineFallback()
    {
        const double toLat = 10.7503;
        const double toLng = 106.6803;
        var expectedDistanceM = (int)Math.Round(
            GeoDistance.HaversineKm(PublishedLat, PublishedLng, toLat, toLng) * 1000d,
            MidpointRounding.AwayFromZero);

        var response = await _client.GetAsync(FormattableString.Invariant(
            $"/api/v1/listings/{PublishedListingId}/travel?to_lat={toLat}&to_lng={toLng}"));
        var payload = await response.Content.ReadFromJsonAsync<ListingTravelEstimatesDto>(ApiJson.Options);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload!.Modes, Has.Count.EqualTo(3));
            Assert.That(payload.Modes.Select(x => x.Source), Is.All.EqualTo(TravelSource.Fallback));
            Assert.That(payload.Modes.Select(x => x.DurationS), Is.All.Null);
            Assert.That(payload.Modes.Select(x => x.DistanceM), Is.All.EqualTo(expectedDistanceM));
        });
    }
}

[TestFixture]
public sealed class TravelRateLimitTests
{
    private static readonly Guid PublishedListingId = TestListingsSeed.IdFor(3);

    private ListingsApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new ListingsApiFactory(services =>
            services.Configure<TravelOptions>(options => options.RateLimitPerMinute = 2));
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
        (_factory.Services.GetRequiredService<IMemoryCache>() as MemoryCache)?.Clear();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task GetTravel_ExceedingPerIpLimit_Returns429Problem()
    {
        var url = FormattableString.Invariant(
            $"/api/v1/listings/{PublishedListingId}/travel?to_lat=10.7503&to_lng=106.6803");

        var first = await _client.GetAsync(url);
        var second = await _client.GetAsync(url);
        var third = await _client.GetAsync(url);
        var code = await ReadProblemCodeAsync(third);

        Assert.Multiple(() =>
        {
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(third.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
            Assert.That(code, Is.EqualTo(TravelProblemFactory.RateLimited));
            Assert.That(third.Headers.Contains("Retry-After"), Is.True);
        });
    }

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code)
            ? code.GetString()
            : null;
    }
}
