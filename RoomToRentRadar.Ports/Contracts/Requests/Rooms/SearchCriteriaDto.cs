namespace RoomToRentRadar.Ports.Contracts.Requests.Rooms;

using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>Matches <c>SearchCriteriaDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record SearchCriteriaDto(
    string CampusId,
    long? MinRentVnd,
    long? MaxRentVnd,
    double? MinAreaM2,
    double? MaxAreaM2,
    IReadOnlyList<RoomType>? RoomTypes,
    double? MaxDistanceKm,
    string Sort,
    IReadOnlyList<GeoPointDto>? Polygon
);