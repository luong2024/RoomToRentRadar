using System.Net;
using System.Text;
using System.Web;
using RoomToRentRadar.Infrastructure.Adapters.Routing;

namespace RoomToRentRadarUnitTests.Infrastructure;

/// <summary>
/// US09 / T09.1 / ADR-007: OpenRouteService HTTP adapter contract tests
/// (driving + walking profiles, lng/lat ordering, 3s timeout fallback, error handling).
/// </summary>
[TestFixture]
public class OpenRouteServiceTravelTimeAdapterTests
{
    private const string ValidOrsJson = """
        {
          "type": "FeatureCollection",
          "features": [
            {
              "type": "Feature",
              "properties": {
                "summary": {
                  "distance": 1842.6,
                  "duration": 312.4
                }
              }
            }
          ]
        }
        """;

    [Test]
    public async Task GetRoutesAsync_ValidResponses_ParsesDrivingAndWalkingAndSendsLngLatOrder()
    {
        var recordedUris = new List<Uri>();
        var recordedAuthHeaders = new List<string?>();
        var handler = new DelegateHttpMessageHandler(async (request, ct) =>
        {
            recordedUris.Add(request.RequestUri!);
            recordedAuthHeaders.Add(request.Headers.TryGetValues("Authorization", out var values)
                ? values.FirstOrDefault()
                : null);

            var isFoot = request.RequestUri!.AbsolutePath.Contains("foot-walking", StringComparison.Ordinal);
            var json = isFoot
                ? ValidOrsJson.Replace("1842.6", "1610.2").Replace("312.4", "1175.8")
                : ValidOrsJson;

            await Task.CompletedTask;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler);
        var settings = new RoutingSettings
        {
            BaseUrl = "https://api.openrouteservice.org",
            OpenRouteServiceApiKey = "test-ors-key",
            TimeoutSeconds = 3
        };
        var sut = new OpenRouteServiceTravelTimeAdapter(httpClient, settings);

        var result = await sut.GetRoutesAsync(21.5875, 105.8080, 21.5851, 105.8066);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.Driving.DistanceMeters, Is.EqualTo(1843));
            Assert.That(result.Driving.DurationSeconds, Is.EqualTo(312));
            Assert.That(result.Walking.DistanceMeters, Is.EqualTo(1610));
            Assert.That(result.Walking.DurationSeconds, Is.EqualTo(1176));

            Assert.That(recordedAuthHeaders, Is.All.EqualTo("test-ors-key"));
            Assert.That(recordedUris.Any(u => u.AbsolutePath.EndsWith("/v2/directions/driving-car", StringComparison.Ordinal)), Is.True);
            Assert.That(recordedUris.Any(u => u.AbsolutePath.EndsWith("/v2/directions/foot-walking", StringComparison.Ordinal)), Is.True);

            var query = HttpUtility.ParseQueryString(recordedUris[0].Query);
            Assert.That(query["start"], Is.EqualTo("105.808,21.5875"));
            Assert.That(query["end"], Is.EqualTo("105.8066,21.5851"));
        });
    }

    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.Unauthorized)]
    public async Task GetRoutesAsync_HttpError_ReturnsNullForFallback(HttpStatusCode status)
    {
        var handler = new DelegateHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(status)));

        using var httpClient = new HttpClient(handler);
        var sut = new OpenRouteServiceTravelTimeAdapter(httpClient, new RoutingSettings
        {
            OpenRouteServiceApiKey = "test-ors-key",
            TimeoutSeconds = 3
        });

        var result = await sut.GetRoutesAsync(21.5875, 105.8080, 21.5851, 105.8066);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetRoutesAsync_MalformedJson_ReturnsNullForFallback()
    {
        var handler = new DelegateHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{not-json", Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler);
        var sut = new OpenRouteServiceTravelTimeAdapter(httpClient, new RoutingSettings
        {
            OpenRouteServiceApiKey = "test-ors-key",
            TimeoutSeconds = 3
        });

        var result = await sut.GetRoutesAsync(21.5875, 105.8080, 21.5851, 105.8066);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetRoutesAsync_ExceedsTimeout_CancelsAndReturnsNullForFallback()
    {
        var handler = new DelegateHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var httpClient = new HttpClient(handler);
        var sut = new OpenRouteServiceTravelTimeAdapter(httpClient, new RoutingSettings
        {
            OpenRouteServiceApiKey = "test-ors-key",
            TimeoutSeconds = 1
        });

        var result = await sut.GetRoutesAsync(21.5875, 105.8080, 21.5851, 105.8066);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetRoutesAsync_CallerCancelled_PropagatesOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = new DelegateHttpMessageHandler((_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        using var httpClient = new HttpClient(handler);
        var sut = new OpenRouteServiceTravelTimeAdapter(httpClient, new RoutingSettings
        {
            OpenRouteServiceApiKey = "test-ors-key",
            TimeoutSeconds = 3
        });

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await sut.GetRoutesAsync(21.5875, 105.8080, 21.5851, 105.8066, cts.Token));
    }

    private sealed class DelegateHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public DelegateHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }
}
