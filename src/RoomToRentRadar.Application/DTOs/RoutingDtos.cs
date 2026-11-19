namespace RoomToRentRadar.Application.DTOs;

/// <summary>
/// Coordinates supplied by a caller to calculate a route.
/// </summary>
public sealed record RouteRequest(
    double? OriginLatitude,
    double? OriginLongitude,
    double? DestinationLatitude,
    double? DestinationLongitude);

/// <summary>
/// A latitude/longitude point in the route geometry returned to map clients.
/// </summary>
public sealed record RouteCoordinate(double Latitude, double Longitude);

/// <summary>
/// Travel metrics and geometry for a calculated route.
/// </summary>
public sealed record RouteResponse(
    double DistanceKm,
    int DurationMinutes,
    IReadOnlyList<RouteCoordinate> RouteCoordinates);

/// <summary>
/// Provider-neutral routing data in the units received from a routing engine.
/// </summary>
public sealed record RoutingProviderResult(
    double DistanceMeters,
    double DurationSeconds,
    IReadOnlyList<RouteCoordinate> RouteCoordinates);
