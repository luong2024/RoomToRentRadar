using System.Globalization;
using System.Text.Json;
using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadar.Infrastructure.Adapters.Routing;

/// <summary>
/// OpenRouteService Directions API v2 adapter (ADR-007, BR-TRV-01..04).
/// Queries <c>driving-car</c> and <c>foot-walking</c> with a strict timeout and returns
/// <c>null</c> when routing is unavailable so callers return the straight-line fallback.
/// </summary>
public sealed class OpenRouteServiceTravelTimeAdapter : ITravelTimePort
{
    private const string DrivingProfile = "driving-car";
    private const string WalkingProfile = "foot-walking";

    private readonly HttpClient _httpClient;
    private readonly RoutingSettings _settings;

    public OpenRouteServiceTravelTimeAdapter(HttpClient httpClient, RoutingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(settings);
        _httpClient = httpClient;
        _settings = settings;
    }

    public async Task<RoutingModesResult?> GetRoutesAsync(
        double originLat,
        double originLng,
        double destinationLat,
        double destinationLng,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(_settings.OpenRouteServiceApiKey))
        {
            return null;
        }

        var timeoutSeconds = _settings.TimeoutSeconds > 0
            ? _settings.TimeoutSeconds
            : RoutingSettings.DefaultTimeoutSeconds;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            var drivingTask = FetchProfileAsync(
                DrivingProfile, originLat, originLng, destinationLat, destinationLng, linkedCts.Token);
            var walkingTask = FetchProfileAsync(
                WalkingProfile, originLat, originLng, destinationLat, destinationLng, linkedCts.Token);

            var legs = await Task.WhenAll(drivingTask, walkingTask).ConfigureAwait(false);
            var driving = legs[0];
            var walking = legs[1];
            if (driving is null || walking is null)
            {
                return null;
            }

            return new RoutingModesResult(driving, walking);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<RouteLegResult?> FetchProfileAsync(
        string profile,
        double originLat,
        double originLng,
        double destinationLat,
        double destinationLng,
        CancellationToken ct)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_settings.BaseUrl)
            ? "https://api.openrouteservice.org"
            : _settings.BaseUrl.TrimEnd('/');

        var start = $"{originLng.ToString(CultureInfo.InvariantCulture)},{originLat.ToString(CultureInfo.InvariantCulture)}";
        var end = $"{destinationLng.ToString(CultureInfo.InvariantCulture)},{destinationLat.ToString(CultureInfo.InvariantCulture)}";
        var url = $"{baseUrl}/v2/directions/{profile}?start={Uri.EscapeDataString(start)}&end={Uri.EscapeDataString(end)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization", _settings.OpenRouteServiceApiKey);
        request.Headers.TryAddWithoutValidation("Accept", "application/geo+json, application/json");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        if (!doc.RootElement.TryGetProperty("features", out var features)
            || features.ValueKind != JsonValueKind.Array
            || features.GetArrayLength() == 0)
        {
            return null;
        }

        var first = features[0];
        if (!first.TryGetProperty("properties", out var properties)
            || !properties.TryGetProperty("summary", out var summary)
            || !summary.TryGetProperty("distance", out var distanceProp)
            || !summary.TryGetProperty("duration", out var durationProp)
            || !distanceProp.TryGetDouble(out var distanceMeters)
            || !durationProp.TryGetDouble(out var durationSeconds)
            || !double.IsFinite(distanceMeters)
            || !double.IsFinite(durationSeconds))
        {
            return null;
        }

        return new RouteLegResult(
            DistanceMeters: Math.Max(0, (int)Math.Round(distanceMeters, MidpointRounding.AwayFromZero)),
            DurationSeconds: Math.Max(0, (int)Math.Round(durationSeconds, MidpointRounding.AwayFromZero)));
    }
}
