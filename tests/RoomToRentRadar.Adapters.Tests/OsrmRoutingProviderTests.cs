using System.Net;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Adapters.Outbound.Routing;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Adapters.Tests;

public class OsrmRoutingProviderTests
{
    private static readonly GeoLocation Origin = new(21.0285, 105.8542);
    private static readonly GeoLocation Destination = new(21.0368, 105.8342);

    [Test]
    public async Task GetRouteAsync_WhenOsrmReturnsGeoJson_ShouldBuildDrivingRequestAndMapCoordinates()
    {
        HttpRequestMessage? capturedRequest = null;
        using var client = CreateClient((request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(JsonResponse("""
                {"code":"Ok","routes":[{"distance":1534.7,"duration":321.2,"geometry":{"coordinates":[[105.8542,21.0285],[105.8342,21.0368]]}}]}
                """));
        });
        var provider = CreateProvider(client);

        var result = await provider.GetRouteAsync(Origin, Destination);

        Assert.Multiple(() =>
        {
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest!.RequestUri!.PathAndQuery, Is.EqualTo("/route/v1/driving/105.8542,21.0285;105.8342,21.0368?overview=full&geometries=geojson&steps=false"));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.DistanceMeters, Is.EqualTo(1534.7d));
            Assert.That(result.DurationSeconds, Is.EqualTo(321.2d));
            Assert.That(result.RouteCoordinates, Is.EqualTo([
                new RoomToRentRadar.Application.DTOs.RouteCoordinate(21.0285, 105.8542),
                new RoomToRentRadar.Application.DTOs.RouteCoordinate(21.0368, 105.8342)]));
        });
    }

    [Test]
    public async Task GetRouteAsync_WhenOsrmReportsNoRoute_ShouldReturnNull()
    {
        using var client = CreateClient((_, _) => Task.FromResult(JsonResponse("""{"code":"NoRoute","routes":[]}""")));
        var provider = CreateProvider(client);

        var result = await provider.GetRouteAsync(Origin, Destination);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetRouteAsync_WhenOsrmReturnsEmptyRoutes_ShouldReturnNull()
    {
        using var client = CreateClient((_, _) => Task.FromResult(JsonResponse("""{"code":"Ok","routes":[]}""")));
        var provider = CreateProvider(client);

        var result = await provider.GetRouteAsync(Origin, Destination);

        Assert.That(result, Is.Null);
    }

    [TestCase("""{"code":"Ok"}""")]
    [TestCase("""{"code":"Ok","routes":{}}""")]
    public void GetRouteAsync_WhenOsrmReturnsInvalidRoutes_ShouldThrowUnavailableException(string json)
    {
        using var client = CreateClient((_, _) => Task.FromResult(JsonResponse(json)));
        var provider = CreateProvider(client);

        Assert.ThrowsAsync<RoutingProviderUnavailableException>(() => provider.GetRouteAsync(Origin, Destination));
    }

    [TestCase("[]", TestName = "GetRouteAsync_WhenOsrmReturnsArrayRoot_ShouldThrowUnavailableException")]
    [TestCase("null", TestName = "GetRouteAsync_WhenOsrmReturnsNullRoot_ShouldThrowUnavailableException")]
    [TestCase("123", TestName = "GetRouteAsync_WhenOsrmReturnsNumberRoot_ShouldThrowUnavailableException")]
    [TestCase("\"invalid\"", TestName = "GetRouteAsync_WhenOsrmReturnsStringRoot_ShouldThrowUnavailableException")]
    public void GetRouteAsync_WhenOsrmReturnsNonObjectRoot_ShouldThrowUnavailableException(string json)
    {
        using var client = CreateClient((_, _) => Task.FromResult(JsonResponse(json)));
        var provider = CreateProvider(client);

        var exception = Assert.ThrowsAsync<RoutingProviderUnavailableException>(
            () => provider.GetRouteAsync(Origin, Destination));

        Assert.That(exception!.InnerException, Is.TypeOf<System.Text.Json.JsonException>());
    }

    [TestCase("""{"code":"Ok","routes":[null]}""")]
    [TestCase("""{"code":"Ok","routes":["not-a-route"]}""")]
    public void GetRouteAsync_WhenOsrmRouteEntryIsNotObject_ShouldThrowUnavailableException(string json)
    {
        using var client = CreateClient((_, _) => Task.FromResult(JsonResponse(json)));
        var provider = CreateProvider(client);

        Assert.ThrowsAsync<RoutingProviderUnavailableException>(() => provider.GetRouteAsync(Origin, Destination));
    }

    [Test]
    public void GetRouteAsync_WhenOsrmReturnsHttpError_ShouldThrowUnavailableException()
    {
        using var client = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var provider = CreateProvider(client);

        Assert.ThrowsAsync<RoutingProviderUnavailableException>(() => provider.GetRouteAsync(Origin, Destination));
    }

    [Test]
    public void GetRouteAsync_WhenTransportTimesOut_ShouldThrowTimeoutException()
    {
        using var client = CreateClient((_, _) => throw new OperationCanceledException());
        var provider = CreateProvider(client);

        Assert.ThrowsAsync<RoutingProviderTimeoutException>(() => provider.GetRouteAsync(Origin, Destination));
    }

    [Test]
    public void GetRouteAsync_WhenCallerCancels_ShouldPreserveCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        using var client = CreateClient((_, cancellationToken) => throw new OperationCanceledException(cancellationToken));
        var provider = CreateProvider(client);

        Assert.CatchAsync<OperationCanceledException>(() => provider.GetRouteAsync(Origin, Destination, cancellationSource.Token));
    }

    private static OsrmRoutingProvider CreateProvider(HttpClient client) =>
        new(client, Options.Create(new RoutingOptions { TimeoutSeconds = 10 }));

    private static HttpClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) =>
        new(new StubHttpMessageHandler(sendAsync)) { BaseAddress = new Uri("https://osrm.test/") };

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json) };

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        {
            _sendAsync = sendAsync;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _sendAsync(request, cancellationToken);
    }
}
