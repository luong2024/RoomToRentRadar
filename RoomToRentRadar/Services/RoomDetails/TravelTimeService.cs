using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using RoomToRentRadar.Infrastructure.Api;

namespace RoomToRentRadar.Services.RoomDetails;

public sealed record TravelInfo(double DistanceKm, int? DurationMinutes, bool IsFallback);

public interface ITravelTimeService
{
    Task<TravelInfo?> GetAsync(string roomId, double? latitude, double? longitude,
        double destinationLatitude, double destinationLongitude, CancellationToken cancellationToken = default);
}

public sealed class TravelTimeService(IApiClient apiClient) : ITravelTimeService
{
    private sealed class RouteResponse
    {
        public double DistanceKm { get; set; }
        public int? DurationMinutes { get; set; }
    }

    public async Task<TravelInfo?> GetAsync(string roomId, double? latitude, double? longitude,
        double destinationLatitude, double destinationLongitude, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId) || !Valid(latitude, longitude) ||
            !Valid(destinationLatitude, destinationLongitude)) return null;

        var endpoint = $"api/rooms/{Uri.EscapeDataString(roomId)}/travel-time?destinationLatitude={destinationLatitude.ToString(CultureInfo.InvariantCulture)}&destinationLongitude={destinationLongitude.ToString(CultureInfo.InvariantCulture)}";
        try
        {
            var response = await apiClient.GetAsync<RouteResponse>(endpoint, cancellationToken);
            if (response is not null && double.IsFinite(response.DistanceKm) && response.DistanceKm >= 0 && response.DurationMinutes is >= 0)
                return new TravelInfo(response.DistanceKm, response.DurationMinutes, false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or NotSupportedException) { }

        cancellationToken.ThrowIfCancellationRequested();
        const double radius = 6371;
        static double Radians(double degrees) => degrees * Math.PI / 180;
        var dLat = Radians(destinationLatitude - latitude!.Value);
        var dLon = Radians(destinationLongitude - longitude!.Value);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(Radians(latitude.Value)) *
            Math.Cos(Radians(destinationLatitude)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return new TravelInfo(2 * radius * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1))), null, true);
    }

    private static bool Valid(double? latitude, double? longitude) =>
        latitude is double lat && longitude is double lon && double.IsFinite(lat) && double.IsFinite(lon) &&
        lat is >= -90 and <= 90 && lon is >= -180 and <= 180;
}
