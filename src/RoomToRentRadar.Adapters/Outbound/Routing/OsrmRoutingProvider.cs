using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Adapters.Outbound.Routing;

/// <summary>
/// Retrieves driving routes from an OSRM-compatible HTTP endpoint.
/// </summary>
public sealed class OsrmRoutingProvider : IRoutingProvider
{
    private readonly HttpClient _httpClient;
    private readonly RoutingOptions _options;

    public OsrmRoutingProvider(HttpClient httpClient, IOptions<RoutingOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<RoutingProviderResult?> GetRouteAsync(
        GeoLocation origin,
        GeoLocation destination,
        CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 1, 120)));

        try
        {
            var requestUri = CreateRequestUri(origin, destination);
            using var response = await _httpClient.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token);

            if (!response.IsSuccessStatusCode)
            {
                throw new RoutingProviderUnavailableException(
                    $"The routing provider returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
            }

            await using var content = await response.Content.ReadAsStreamAsync(timeoutSource.Token);
            using var document = await JsonDocument.ParseAsync(content, cancellationToken: timeoutSource.Token);
            return ParseRoute(document.RootElement);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            throw new RoutingProviderTimeoutException(ex);
        }
        catch (HttpRequestException ex)
        {
            throw new RoutingProviderUnavailableException("The routing provider could not be reached.", ex);
        }
        catch (JsonException ex)
        {
            throw new RoutingProviderUnavailableException("The routing provider returned an invalid response.", ex);
        }
    }

    private string CreateRequestUri(GeoLocation origin, GeoLocation destination)
    {
        var profile = string.IsNullOrWhiteSpace(_options.Profile) ? "driving" : _options.Profile.Trim();
        return $"route/v1/{Uri.EscapeDataString(profile)}/{FormatCoordinate(origin.Longitude)},{FormatCoordinate(origin.Latitude)};{FormatCoordinate(destination.Longitude)},{FormatCoordinate(destination.Latitude)}?overview=full&geometries=geojson&steps=false";
    }

    private static RoutingProviderResult? ParseRoute(JsonElement root)
    {
        if (!root.TryGetProperty("code", out var codeElement) || codeElement.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("OSRM response did not include a status code.");
        }

        var code = codeElement.GetString();
        if (string.Equals(code, "NoRoute", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!string.Equals(code, "Ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new JsonException($"OSRM returned status '{code}'.");
        }

        if (!root.TryGetProperty("routes", out var routesElement) || routesElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("OSRM response did not include a valid routes array.");
        }

        if (routesElement.GetArrayLength() == 0)
        {
            return null;
        }

        var route = routesElement[0];
        if (route.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("OSRM routes array contains an invalid route.");
        }

        var distanceMeters = GetFiniteNonNegativeNumber(route, "distance");
        var durationSeconds = GetFiniteNonNegativeNumber(route, "duration");

        if (!route.TryGetProperty("geometry", out var geometry) || geometry.ValueKind != JsonValueKind.Object ||
            !geometry.TryGetProperty("coordinates", out var coordinates) || coordinates.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("OSRM route geometry was missing or invalid.");
        }

        var routeCoordinates = new List<RouteCoordinate>(coordinates.GetArrayLength());
        foreach (var coordinate in coordinates.EnumerateArray())
        {
            if (coordinate.ValueKind != JsonValueKind.Array || coordinate.GetArrayLength() < 2)
            {
                throw new JsonException("OSRM route geometry contains an invalid coordinate.");
            }

            var longitude = GetFiniteNumber(coordinate[0]);
            var latitude = GetFiniteNumber(coordinate[1]);
            if (latitude is < -90d or > 90d || longitude is < -180d or > 180d)
            {
                throw new JsonException("OSRM route geometry contains an out-of-range coordinate.");
            }

            routeCoordinates.Add(new RouteCoordinate(latitude, longitude));
        }

        return new RoutingProviderResult(distanceMeters, durationSeconds, routeCoordinates);
    }

    private static double GetFiniteNonNegativeNumber(JsonElement source, string propertyName)
    {
        if (!source.TryGetProperty(propertyName, out var element))
        {
            throw new JsonException($"OSRM route did not include '{propertyName}'.");
        }

        var value = GetFiniteNumber(element);
        if (value < 0)
        {
            throw new JsonException($"OSRM route '{propertyName}' cannot be negative.");
        }

        return value;
    }

    private static double GetFiniteNumber(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetDouble(out var value) || !double.IsFinite(value))
        {
            throw new JsonException("OSRM response contains a non-finite number.");
        }

        return value;
    }

    private static string FormatCoordinate(double coordinate) => coordinate.ToString("R", CultureInfo.InvariantCulture);
}
