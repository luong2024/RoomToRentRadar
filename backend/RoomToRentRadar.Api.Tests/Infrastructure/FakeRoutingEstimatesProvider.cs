using RoomToRentRadar.Ports.Models.Travel;
using RoomToRentRadar.Ports.Outbound.Services;
using RoomToRentRadar.Shared.Travel;

namespace RoomToRentRadar.Api.Tests.Infrastructure;

public sealed class FakeRoutingEstimatesProvider : IRoutingEstimatesProvider
{
    private readonly object _gate = new();
    private readonly List<Call> _calls = [];

    public Func<double, double, double, double, TravelMode, RoutingEstimate?>? Handler { get; set; }

    public int CallCount
    {
        get
        {
            lock (_gate)
            {
                return _calls.Count;
            }
        }
    }

    public IReadOnlyList<Call> Calls
    {
        get
        {
            lock (_gate)
            {
                return _calls.ToList();
            }
        }
    }

    public Task<RoutingEstimate?> GetEstimateAsync(
        double fromLat,
        double fromLng,
        double toLat,
        double toLng,
        TravelMode mode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _calls.Add(new Call(fromLat, fromLng, toLat, toLng, mode));
        }

        return Task.FromResult(Handler?.Invoke(fromLat, fromLng, toLat, toLng, mode));
    }

    public void Reset()
    {
        lock (_gate)
        {
            _calls.Clear();
        }

        Handler = null;
    }

    public sealed record Call(double FromLat, double FromLng, double ToLat, double ToLng, TravelMode Mode);
}
