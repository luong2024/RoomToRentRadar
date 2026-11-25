using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadar.Infrastructure.Adapters.Mocks;

/// <summary>
/// Development-only deterministic routing mock (ADR-007, GUARDRAILS §1.5.4).
/// Simulates road routing distances and durations from Haversine distance when ORS is mocked.
/// </summary>
public sealed class MockTravelTimeAdapter : ITravelTimePort
{
    private const double DrivingDetourFactor = 1.3;
    private const double WalkingDetourFactor = 1.15;
    private const double MotorbikeSpeedKmPerHour = 30.0;
    private const double WalkingSpeedKmPerHour = 5.0;

    public Task<RoutingModesResult?> GetRoutesAsync(
        double originLat,
        double originLng,
        double destinationLat,
        double destinationLng,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var straightKm = HaversineDistanceCalculator.CalculateDistanceKm(
            originLat, originLng, destinationLat, destinationLng);
        if (!double.IsFinite(straightKm))
        {
            return Task.FromResult<RoutingModesResult?>(null);
        }

        var effectiveKm = Math.Max(0.2, straightKm);
        var drivingMeters = Math.Max(200, (int)Math.Round(effectiveKm * DrivingDetourFactor * 1000.0, MidpointRounding.AwayFromZero));
        var drivingSeconds = Math.Max(60, (int)Math.Round((drivingMeters / 1000.0) / MotorbikeSpeedKmPerHour * 3600.0, MidpointRounding.AwayFromZero));

        var walkingMeters = Math.Max(150, (int)Math.Round(effectiveKm * WalkingDetourFactor * 1000.0, MidpointRounding.AwayFromZero));
        var walkingSeconds = Math.Max(120, (int)Math.Round((walkingMeters / 1000.0) / WalkingSpeedKmPerHour * 3600.0, MidpointRounding.AwayFromZero));

        var result = new RoutingModesResult(
            Driving: new RouteLegResult(drivingMeters, drivingSeconds),
            Walking: new RouteLegResult(walkingMeters, walkingSeconds));

        return Task.FromResult<RoutingModesResult?>(result);
    }
}
