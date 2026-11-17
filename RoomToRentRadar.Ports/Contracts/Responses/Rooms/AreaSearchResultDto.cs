namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>Matches <c>AreaSearchResultDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record AreaSearchResultDto(
    IReadOnlyList<RoomCardDto> Items,
    int TotalCount,
    bool Truncated
);