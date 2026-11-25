using System.Net;
using System.Text;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Services.RoomDetails;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class TravelTimeServiceTests
{
    [Test]
    public async Task GetAsync_ValidRoute_ReturnsDrivingMetrics()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"distanceKm\":3.4,\"durationMinutes\":10}", Encoding.UTF8, "application/json")
        }));
        var service = CreateService(handler);

        var result = await service.GetAsync("room-1", 21.5, 105.8, 21.6, 105.9);

        Assert.Multiple(() =>
        {
            Assert.That(result!.DistanceKm, Is.EqualTo(3.4));
            Assert.That(result.DurationMinutes, Is.EqualTo(10));
            Assert.That(result.IsFallback, Is.False);
            Assert.That(handler.LastUri!.AbsolutePath, Is.EqualTo("/api/rooms/room-1/travel-time"));
        });
    }

    [Test]
    public async Task GetAsync_RoutingUnavailable_ReturnsStraightLineWithoutDuration()
    {
        var service = CreateService(new StubHandler((_, _) => throw new HttpRequestException("offline")));
        var result = await service.GetAsync("room-1", 0, 0, 0, 1);
        Assert.Multiple(() =>
        {
            Assert.That(result!.DistanceKm, Is.EqualTo(111.195).Within(0.01));
            Assert.That(result.DurationMinutes, Is.Null);
            Assert.That(result.IsFallback, Is.True);
        });
    }

    [Test]
    public async Task GetAsync_InvalidCoordinates_DoesNotCallApi()
    {
        var handler = new StubHandler((_, _) => throw new AssertionException("API should not be called"));
        var result = await CreateService(handler).GetAsync("room-1", null, 105, 21, 105);
        Assert.That(result, Is.Null);
        Assert.That(handler.LastUri, Is.Null);
    }

    [Test]
    public async Task GetAsync_InvalidJsonAndMatchingLocations_ReturnsZeroFallbackDistance()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not json", Encoding.UTF8, "application/json")
        }));
        var result = await CreateService(handler).GetAsync("room-1", 21, 105, 21, 105);
        Assert.Multiple(() =>
        {
            Assert.That(result!.DistanceKm, Is.Zero);
            Assert.That(result.DurationMinutes, Is.Null);
            Assert.That(result.IsFallback, Is.True);
        });
    }

    [Test]
    public void GetAsync_CallerCancellation_DoesNotReturnFallback()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = CreateService(new StubHandler((_, token) => Task.FromCanceled<HttpResponseMessage>(token)));
        Assert.ThrowsAsync<TaskCanceledException>(async () => await service.GetAsync("room-1", 21, 105, 22, 106, cancellation.Token));
    }

    private static TravelTimeService CreateService(StubHandler handler) =>
        new(new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            return send(request, cancellationToken);
        }
    }
}
