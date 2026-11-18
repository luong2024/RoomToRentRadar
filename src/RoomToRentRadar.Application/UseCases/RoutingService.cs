using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Application.UseCases;

public sealed class RoutingService : IRoutingService
{
    private readonly IRoutingProvider _routingProvider;
    private readonly IRoomRepository _roomRepository;

    public RoutingService(IRoutingProvider routingProvider, IRoomRepository roomRepository)
    {
        _routingProvider = routingProvider ?? throw new ArgumentNullException(nameof(routingProvider));
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
    }

    public async Task<RouteResponse> GetRouteAsync(RouteRequest? request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ValidationException("request", "Route request is required.");
        }

        var origin = CreateLocation(request.OriginLatitude, request.OriginLongitude, "origin");
        var destination = CreateLocation(request.DestinationLatitude, request.DestinationLongitude, "destination");
        return await CalculateRouteAsync(origin, destination, cancellationToken);
    }

    public async Task<RouteResponse> GetRouteForRoomAsync(
        string roomId,
        double? destinationLatitude,
        double? destinationLongitude,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new ValidationException("roomId", "Room ID is required.");
        }

        var room = await _roomRepository.GetByIdAsync(roomId.Trim(), cancellationToken)
            ?? throw new NotFoundException($"Room with ID '{roomId.Trim()}' was not found.");

        var origin = CreateLocation(room.Latitude, room.Longitude, "room");
        var destination = CreateLocation(destinationLatitude, destinationLongitude, "destination");
        return await CalculateRouteAsync(origin, destination, cancellationToken);
    }

    private async Task<RouteResponse> CalculateRouteAsync(
        GeoLocation origin,
        GeoLocation destination,
        CancellationToken cancellationToken)
    {
        var route = await _routingProvider.GetRouteAsync(origin, destination, cancellationToken)
            ?? throw new RouteNotFoundException();

        if (!double.IsFinite(route.DistanceMeters) || route.DistanceMeters < 0 ||
            !double.IsFinite(route.DurationSeconds) || route.DurationSeconds < 0)
        {
            throw new RoutingProviderUnavailableException("The routing provider returned invalid route metrics.");
        }

        return new RouteResponse(
            DistanceKm: Math.Round(route.DistanceMeters / 1000d, 2, MidpointRounding.AwayFromZero),
            DurationMinutes: (int)Math.Ceiling(route.DurationSeconds / 60d),
            RouteCoordinates: route.RouteCoordinates ?? Array.Empty<RouteCoordinate>());
    }

    private static GeoLocation CreateLocation(double? latitude, double? longitude, string prefix)
    {
        if (!latitude.HasValue || !longitude.HasValue)
        {
            throw new ValidationException(prefix, $"{ToDisplayName(prefix)} latitude and longitude are required.");
        }

        if (!double.IsFinite(latitude.Value) || latitude.Value is < -90d or > 90d)
        {
            throw new ValidationException($"{prefix}Latitude", $"{ToDisplayName(prefix)} latitude must be between -90 and 90.");
        }

        if (!double.IsFinite(longitude.Value) || longitude.Value is < -180d or > 180d)
        {
            throw new ValidationException($"{prefix}Longitude", $"{ToDisplayName(prefix)} longitude must be between -180 and 180.");
        }

        return new GeoLocation(latitude.Value, longitude.Value);
    }

    private static string ToDisplayName(string prefix) => char.ToUpperInvariant(prefix[0]) + prefix[1..];
}
