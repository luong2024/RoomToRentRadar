using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoomToRentRadar.SqliteDataAccess;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Api.Features.Travel;
using RoomToRentRadar.Ports.Models.Travel;
using RoomToRentRadar.Ports.Outbound.Services;
using RoomToRentRadar.UseCases.Listings;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Campuses;
using RoomToRentRadar.Shared.Serialization;
using RoomToRentRadar.Shared.Travel;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class TravelEndpointTests
{
    // Index 0 of TestListingsSeed is Guid.Empty, which EF treats as an unset key
    // and replaces on insert, so published-listing tests use index 3.
    private static readonly Guid PublishedListingId = TestListingsSeed.IdFor(3);
    private static readonly CampusInfo BachKhoa = CampusCatalog.FindById("bach-khoa")!;
    private static readonly CampusInfo Khtn = CampusCatalog.FindById("khtn")!;
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
            // The limiter partition is per-IP and shared across the fixture run.
            services.Configure<TravelOptions>(options => options.RateLimitPerMinute = 1000);
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

        var response = await GetTravelAsync(PublishedListingId, BachKhoa.Id);
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
    public async Task GetTravel_KnownCampus_UsesServerSideCampusCoordinates()
    {
        _provider.Handler = (_, _, _, _, _) => new RoutingEstimate(1000, 60);

        var response = await GetTravelAsync(PublishedListingId, Khtn.Id);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        Assert.Multiple(() =>
        {
            Assert.That(_provider.Calls, Is.Not.Empty);
            Assert.That(_provider.Calls.Select(call => call.ToLat), Is.All.EqualTo(Khtn.Lat));
            Assert.That(_provider.Calls.Select(call => call.ToLng), Is.All.EqualTo(Khtn.Lng));
            Assert.That(_provider.Calls.Select(call => call.FromLat), Is.All.EqualTo(PublishedLat));
            Assert.That(_provider.Calls.Select(call => call.FromLng), Is.All.EqualTo(PublishedLng));
        });
    }

    [Test]
    public async Task GetTravel_RouteResponse_UsesSnakeCaseWireValues()
    {
        _provider.Handler = (_, _, _, _, _) => new RoutingEstimate(1000, 60);

        var response = await GetTravelAsync(PublishedListingId, BachKhoa.Id);
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

        var first = await ReadPayloadAsync(await GetTravelAsync(listingId, BachKhoa.Id));
        var second = await ReadPayloadAsync(await GetTravelAsync(listingId, BachKhoa.Id));

        Assert.Multiple(() =>
        {
            Assert.That(_provider.CallCount, Is.EqualTo(3), "the second request must be served from the cache");
            Assert.That(
                second.Modes.Select(x => x.DistanceM),
                Is.EqualTo(first.Modes.Select(x => x.DistanceM)));
        });
    }

    [Test]
    public async Task GetTravel_SwitchingCampus_FetchesNewEstimates()
    {
        _provider.Handler = (_, _, toLat, _, _) => new RoutingEstimate((int)(toLat * 100), 60);

        var bachKhoaPayload = await ReadPayloadAsync(
            await GetTravelAsync(PublishedListingId, BachKhoa.Id));
        var khtnPayload = await ReadPayloadAsync(
            await GetTravelAsync(PublishedListingId, Khtn.Id));

        Assert.Multiple(() =>
        {
            Assert.That(_provider.CallCount, Is.EqualTo(6));
            Assert.That(
                bachKhoaPayload.Modes[0].DistanceM,
                Is.EqualTo((int)(BachKhoa.Lat * 100)));
            Assert.That(
                khtnPayload.Modes[0].DistanceM,
                Is.EqualTo((int)(Khtn.Lat * 100)));
        });
    }

    [Test]
    public async Task GetTravel_ProviderHasNoBusRoute_FallsBackToStraightLineForBusOnly()
    {
        _provider.Handler = (_, _, _, _, mode) => mode == TravelMode.Bus
            ? null
            : new RoutingEstimate(4200, 900);

        var payload = await ReadPayloadAsync(await GetTravelAsync(PublishedListingId, Khtn.Id));
        var bus = payload.Modes.Single(x => x.Mode == TravelMode.Bus);
        var expectedDistanceM = (int)Math.Round(
            GeoDistance.HaversineKm(PublishedLat, PublishedLng, Khtn.Lat, Khtn.Lng) * 1000d,
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

        var response = await GetTravelAsync(PublishedListingId, BachKhoa.Id);
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

        var first = await ReadPayloadAsync(await GetTravelAsync(PublishedListingId, Khtn.Id));
        var second = await ReadPayloadAsync(await GetTravelAsync(PublishedListingId, Khtn.Id));

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

    [Test]
    public async Task GetTravel_ConcurrentRequests_FetchesEachModeOnce()
    {
        _provider.Handler = (_, _, _, _, _) =>
        {
            Thread.Sleep(75);
            return new RoutingEstimate(3000, 600);
        };

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 10)
                .Select(_ => _client.GetAsync(TravelUrl(PublishedListingId, BachKhoa.Id))));

        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    responses.Select(response => response.StatusCode),
                    Is.All.EqualTo(HttpStatusCode.OK));
                Assert.That(
                    _provider.CallCount,
                    Is.EqualTo(3),
                    "concurrent requests for the same key must share one provider fetch per mode");
            });
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [TestCase(20)]
    [TestCase(21)]
    [TestCase(22)]
    [TestCase(23)]
    [TestCase(24)]
    public async Task GetTravel_NonPublishedListing_ReturnsNotFound(int seedIndex)
    {
        var response = await GetTravelAsync(TestListingsSeed.IdFor(seedIndex), BachKhoa.Id);
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
        var response = await GetTravelAsync(Guid.NewGuid(), BachKhoa.Id);
        var code = await ReadProblemCodeAsync(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(code, Is.EqualTo(ListingProblemFactory.ListingNotFound));
        });
    }

    [Test]
    public async Task GetTravel_MissingCampusId_ReturnsInvalidDestination()
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

    [TestCase("hcmus")]
    [TestCase("")]
    [TestCase("%20%20")]
    [TestCase("bach%20khoa")]
    public async Task GetTravel_UnknownCampusId_ReturnsInvalidDestination(string campusId)
    {
        var response = await _client.GetAsync(
            $"/api/v1/listings/{PublishedListingId}/travel?campus_id={campusId}");
        var code = await ReadProblemCodeAsync(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(code, Is.EqualTo(TravelProblemFactory.InvalidDestination));
        });
        Assert.That(_provider.CallCount, Is.Zero);
    }

    [Test]
    public async Task GetTravel_ExtraCoordinateParameters_AreIgnored()
    {
        _provider.Handler = (_, _, _, _, _) => new RoutingEstimate(1000, 60);

        var response = await _client.GetAsync(
            $"/api/v1/listings/{PublishedListingId}/travel?campus_id={Khtn.Id}&to_lat=0&to_lng=0");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(_provider.Calls.Select(call => call.ToLat), Is.All.EqualTo(Khtn.Lat));
            Assert.That(_provider.Calls.Select(call => call.ToLng), Is.All.EqualTo(Khtn.Lng));
        });
    }

    private Task<HttpResponseMessage> GetTravelAsync(Guid listingId, string campusId)
        => _client.GetAsync(TravelUrl(listingId, campusId));

    private static string TravelUrl(Guid listingId, string campusId)
        => $"/api/v1/listings/{listingId}/travel?campus_id={Uri.EscapeDataString(campusId)}";

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
    private static readonly CampusInfo BachKhoa = CampusCatalog.FindById("bach-khoa")!;
    private const double PublishedLat = 10.703;
    private const double PublishedLng = 106.703;

    private ListingsApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Keep the real provider but guarantee it has no key, so every call falls back.
        _factory = new ListingsApiFactory(services =>
            services.Configure<TravelOptions>(options =>
            {
                options.GoogleRoutesApiKey = null;
                options.RateLimitPerMinute = 1000;
            }));
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
        var expectedDistanceM = (int)Math.Round(
            GeoDistance.HaversineKm(PublishedLat, PublishedLng, BachKhoa.Lat, BachKhoa.Lng) * 1000d,
            MidpointRounding.AwayFromZero);

        var response = await _client.GetAsync(
            $"/api/v1/listings/{PublishedListingId}/travel?campus_id={BachKhoa.Id}");
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
        var url = $"/api/v1/listings/{PublishedListingId}/travel?campus_id=bach-khoa";

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
