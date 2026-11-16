namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>Matches <c>TravelTimesDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record TravelTimesDto(
    string CampusId,
    double StraightLineDistanceKm,
    bool IsFallback,
    IReadOnlyList<TravelTimeItemDto> Items
);