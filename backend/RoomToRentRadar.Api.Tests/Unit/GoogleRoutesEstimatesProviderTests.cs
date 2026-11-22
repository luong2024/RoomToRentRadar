using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Infrastructure.Travel;
using RoomToRentRadar.Ports.Models.Travel;
using RoomToRentRadar.Shared.Travel;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class GoogleRoutesEstimatesProviderTests
{
    [TestCase(TravelMode.Motorbike, "DRIVE")]
    [TestCase(TravelMode.Bus, "TRANSIT")]
    [TestCase(TravelMode.Walking, "WALK")]
    public async Task GetEstimateAsync_SuccessfulRoute_SendsModeAndParsesEstimate(
        TravelMode mode,
        string expectedRoutesMode)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(
            """{"routes":[{"distanceMeters":6200,"duration":"1080s"}]}""")));
        var provider = CreateProvider(handler, apiKey: "test-key");

        var estimate = await provider.GetEstimateAsync(
            10.7,
            106.7,
            10.77,
            106.65,
            mode,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(estimate, Is.EqualTo(new RoutingEstimate(6200, 1080)));
            Assert.That(handler.RequestCount, Is.EqualTo(1));
            Assert.That(handler.LastRequestUri?.AbsolutePath, Is.EqualTo("/directions/v2:computeRoutes"));
            Assert.That(handler.LastApiKey, Is.EqualTo("test-key"));
            Assert.That(handler.LastFieldMask, Is.EqualTo("routes.distanceMeters,routes.duration"));
            Assert.That(handler.LastBody, Does.Contain($"\"travelMode\":\"{expectedRoutesMode}\""));
            Assert.That(handler.LastBody, Does.Contain("\"latitude\":10.7"));
            Assert.That(handler.LastBody, Does.Contain("\"longitude\":106.65"));
        });
    }

    [Test]
    public async Task GetEstimateAsync_MissingApiKey_ReturnsNullWithoutCallingTheProvider()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            JsonResponse("""{"routes":[{"distanceMeters":1,"duration":"1s"}]}""")));
        var provider = CreateProvider(handler, apiKey: null);

        var estimate = await provider.GetEstimateAsync(
            10.7,
            106.7,
            10.77,
            106.65,
            TravelMode.Walking,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(estimate, Is.Null);
            Assert.That(handler.RequestCount, Is.Zero);
        });
    }

    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    public async Task GetEstimateAsync_NonSuccessStatus_ReturnsNull(HttpStatusCode statusCode)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(statusCode)));
        var provider = CreateProvider(handler, apiKey: "test-key");

        var estimate = await provider.GetEstimateAsync(
            10.7,
            106.7,
            10.77,
            106.65,
            TravelMode.Bus,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(estimate, Is.Null);
            Assert.That(handler.RequestCount, Is.EqualTo(1));
        });
    }

    [TestCase("""{"routes":[]}""")]
    [TestCase("""{}""")]
    [TestCase("""{"routes":[{"duration":"900s"}]}""")]
    [TestCase("""{"routes":[{"distanceMeters":0,"duration":"900s"}]}""")]
    [TestCase("""{"routes":[{"distanceMeters":500}]}""")]
    [TestCase("""{"routes":[{"distanceMeters":500,"duration":"soon"}]}""")]
    [TestCase("""{"routes":[{"distanceMeters":500,"duration":"0s"}]}""")]
    [TestCase("not json")]
    public async Task GetEstimateAsync_NoUsableRoute_ReturnsNull(string responseBody)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(responseBody)));
        var provider = CreateProvider(handler, apiKey: "test-key");

        var estimate = await provider.GetEstimateAsync(
            10.7,
            106.7,
            10.77,
            106.65,
            TravelMode.Walking,
            CancellationToken.None);

        Assert.That(estimate, Is.Null);
    }

    [Test]
    public async Task GetEstimateAsync_CallerCancellation_PropagatesOperationCanceled()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        });
        var provider = CreateProvider(handler, apiKey: "test-key");

        Assert.That(
            async () => await provider.GetEstimateAsync(
                10.7,
                106.7,
                10.77,
                106.65,
                TravelMode.Walking,
                cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task GetEstimateAsync_ProviderTimeout_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new TaskCanceledException("timed out"));
        var provider = CreateProvider(handler, apiKey: "test-key");

        var estimate = await provider.GetEstimateAsync(
            10.7,
            106.7,
            10.77,
            106.65,
            TravelMode.Walking,
            CancellationToken.None);

        Assert.That(estimate, Is.Null);
    }

    [Test]
    public async Task GetEstimateAsync_UnexpectedException_Propagates()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("unexpected"));
        var provider = CreateProvider(handler, apiKey: "test-key");

        Assert.That(
            async () => await provider.GetEstimateAsync(
                10.7,
                106.7,
                10.77,
                106.65,
                TravelMode.Walking,
                CancellationToken.None),
            Throws.TypeOf<InvalidOperationException>());
    }

    private static GoogleRoutesEstimatesProvider CreateProvider(HttpMessageHandler handler, string? apiKey)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://routes.googleapis.com/")
        };
        return new GoogleRoutesEstimatesProvider(
            client,
            Options.Create(new TravelOptions { GoogleRoutesApiKey = apiKey }),
            NullLogger<GoogleRoutesEstimatesProvider>.Instance);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        {
            _respond = respond;
        }

        public int RequestCount { get; private set; }

        public Uri? LastRequestUri { get; private set; }

        public string? LastApiKey { get; private set; }

        public string? LastFieldMask { get; private set; }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestUri = request.RequestUri;
            LastApiKey = ReadHeader(request, "X-Goog-Api-Key");
            LastFieldMask = ReadHeader(request, "X-Goog-FieldMask");
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return await _respond(request, cancellationToken);
        }

        private static string? ReadHeader(HttpRequestMessage request, string name)
            => request.Headers.TryGetValues(name, out var values)
                ? values.SingleOrDefault()
                : null;
    }
}
