using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Application.Tests;

public class RoutingServiceTests
{
    private FakeRoutingProvider _routingProvider = null!;
    private FakeRoomRepository _roomRepository = null!;
    private RoutingService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _routingProvider = new FakeRoutingProvider();
        _roomRepository = new FakeRoomRepository();
        _service = new RoutingService(_routingProvider, _roomRepository);
    }

    [Test]
    public async Task GetRouteAsync_WhenCoordinatesAreValid_ShouldConvertProviderMetricsAndReturnGeometry()
    {
        _routingProvider.Result = new RoutingProviderResult(
            2540d,
            125d,
            [new RouteCoordinate(21.0285, 105.8542), new RouteCoordinate(21.0368, 105.8342)]);

        var result = await _service.GetRouteAsync(new RouteRequest(21.0285, 105.8542, 21.0368, 105.8342));

        Assert.Multiple(() =>
        {
            Assert.That(result.DistanceKm, Is.EqualTo(2.54d));
            Assert.That(result.DurationMinutes, Is.EqualTo(3));
            Assert.That(result.RouteCoordinates, Has.Count.EqualTo(2));
            Assert.That(_routingProvider.Origin, Is.EqualTo(new GeoLocation(21.0285, 105.8542)));
            Assert.That(_routingProvider.Destination, Is.EqualTo(new GeoLocation(21.0368, 105.8342)));
        });
    }

    [TestCase(91d, 105d, 21d, 105d)]
    [TestCase(21d, 181d, 21d, 105d)]
    [TestCase(21d, 105d, -91d, 105d)]
    [TestCase(21d, 105d, 21d, -181d)]
    public void GetRouteAsync_WhenCoordinatesAreOutOfRange_ShouldThrowValidationException(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude)
    {
        Assert.ThrowsAsync<ValidationException>(() => _service.GetRouteAsync(
            new RouteRequest(originLatitude, originLongitude, destinationLatitude, destinationLongitude)));
    }

    [Test]
    public void GetRouteAsync_WhenProviderFindsNoRoute_ShouldThrowRouteNotFoundException()
    {
        _routingProvider.Result = null;

        Assert.ThrowsAsync<RouteNotFoundException>(() => _service.GetRouteAsync(
            new RouteRequest(21.0285, 105.8542, 21.0368, 105.8342)));
    }

    [Test]
    public void GetRouteForRoomAsync_WhenRoomDoesNotExist_ShouldThrowNotFoundException()
    {
        Assert.ThrowsAsync<NotFoundException>(() => _service.GetRouteForRoomAsync("missing", 21.0368, 105.8342));
    }

    [Test]
    public void GetRouteForRoomAsync_WhenRoomHasNoCoordinates_ShouldThrowValidationException()
    {
        _roomRepository.Room = new Room
        {
            Id = "room-1",
            Status = RoomStatus.Available,
            IsVerified = true
        };

        Assert.ThrowsAsync<ValidationException>(() => _service.GetRouteForRoomAsync("room-1", 21.0368, 105.8342));
    }

    [TestCase(RoomStatus.Pending, true)]
    [TestCase(RoomStatus.UnderMaintenance, true)]
    [TestCase(RoomStatus.Available, false)]
    public void GetRouteForRoomAsync_WhenRoomIsNotPublic_ShouldHideItAndSkipRoutingProvider(
        RoomStatus status,
        bool isVerified)
    {
        _roomRepository.Room = new Room
        {
            Id = "room-1",
            Latitude = 21.0285,
            Longitude = 105.8542,
            Status = status,
            IsVerified = isVerified
        };

        Assert.ThrowsAsync<NotFoundException>(() => _service.GetRouteForRoomAsync("room-1", 21.0368, 105.8342));
        Assert.That(_routingProvider.CallCount, Is.Zero);
    }

    [Test]
    public void GetRouteAsync_WhenCallerCancels_ShouldPreserveCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        _routingProvider.Exception = new OperationCanceledException(cancellationSource.Token);

        Assert.ThrowsAsync<OperationCanceledException>(() => _service.GetRouteAsync(
            new RouteRequest(21.0285, 105.8542, 21.0368, 105.8342), cancellationSource.Token));
    }

    private sealed class FakeRoutingProvider : IRoutingProvider
    {
        public RoutingProviderResult? Result { get; set; }
        public Exception? Exception { get; set; }
        public GeoLocation? Origin { get; private set; }
        public GeoLocation? Destination { get; private set; }
        public int CallCount { get; private set; }

        public Task<RoutingProviderResult?> GetRouteAsync(GeoLocation origin, GeoLocation destination, CancellationToken cancellationToken = default)
        {
            CallCount++;
            Origin = origin;
            Destination = destination;
            if (Exception is not null)
            {
                return Task.FromException<RoutingProviderResult?>(Exception);
            }

            return Task.FromResult(Result);
        }
    }

    private sealed class FakeRoomRepository : IRoomRepository
    {
        public Room? Room { get; set; }

        public Task<Room?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(Room);

        public Task<IReadOnlyList<Room>> GetSimilarRoomsAsync(string roomId, decimal price, double area, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Room>>(Array.Empty<Room>());

        public Task<(IReadOnlyList<Room> Items, int TotalCount)> SearchAsync(
            RoomToRentRadar.Application.DTOs.SearchRoomsQuery query,
            CancellationToken ct = default) =>
            Task.FromResult<(IReadOnlyList<Room> Items, int TotalCount)>((Array.Empty<Room>(), 0));
    }
}
