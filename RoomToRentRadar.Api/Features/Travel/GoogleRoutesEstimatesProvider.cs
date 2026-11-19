using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Polly.Timeout;
using RoomToRentRadar.Shared.Travel;

namespace RoomToRentRadar.Api.Features.Travel;

public sealed class GoogleRoutesEstimatesProvider(
    HttpClient httpClient,
    IOptions<TravelOptions> options,
    ILogger<GoogleRoutesEstimatesProvider> logger) : IRoutingEstimatesProvider
{
    private const string FieldMask = "routes.distanceMeters,routes.duration";

    public async Task<RoutingEstimate?> GetEstimateAsync(
        double fromLat,
        double fromLng,
        double toLat,
        double toLng,
        TravelMode mode,
        CancellationToken cancellationToken)
    {
        var apiKey = options.Value.GoogleRoutesApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var body = new
        {
            origin = Location(fromLat, fromLng),
            destination = Location(toLat, toLng),
            travelMode = MapTravelMode(mode)
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "directions/v2:computeRoutes")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-Goog-Api-Key", apiKey);
        request.Headers.Add("X-Goog-FieldMask", FieldMask);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Routes provider returned status {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return ParseRoute(document.RootElement);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            logger.LogWarning(exception, "Routes provider call timed out.");
            return null;
        }
        catch (TimeoutRejectedException exception)
        {
            logger.LogWarning(exception, "Routes provider call timed out.");
            return null;
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Routes provider request failed.");
            return null;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Routes provider returned invalid JSON.");
            return null;
        }
    }

    private static object Location(double latitude, double longitude) => new
    {
        location = new
        {
            latLng = new { latitude, longitude }
        }
    };

    // Motorbike is approximated with DRIVE: Routes has no scooter mode for the target market.
    internal static string MapTravelMode(TravelMode mode) => mode switch
    {
        TravelMode.Motorbike => "DRIVE",
        TravelMode.Bus => "TRANSIT",
        TravelMode.Walking => "WALK",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    internal static RoutingEstimate? ParseRoute(JsonElement root)
    {
        if (!root.TryGetProperty("routes", out var routes)
            || routes.ValueKind != JsonValueKind.Array
            || routes.GetArrayLength() == 0)
        {
            return null;
        }

        var route = routes[0];

        if (!route.TryGetProperty("distanceMeters", out var distanceElement)
            || !distanceElement.TryGetInt32(out var distanceM)
            || distanceM <= 0)
        {
            return null;
        }

        if (!route.TryGetProperty("duration", out var durationElement)
            || durationElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var durationText = durationElement.GetString();
        if (durationText is null
            || !durationText.EndsWith('s')
            || !int.TryParse(
                durationText.AsSpan(0, durationText.Length - 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var durationS)
            || durationS <= 0)
        {
            return null;
        }

        return new RoutingEstimate(distanceM, durationS);
    }
}
