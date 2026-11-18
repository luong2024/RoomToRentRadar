namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>Matches <c>ComparisonDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record ComparisonDto(
    string CampusId,
    IReadOnlyList<CompareRoomDto> Rooms
);