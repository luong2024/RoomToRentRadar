namespace RoomToRentRadar.Ports.Contracts.Responses.SavedSearches;

using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;

/// <summary>Matches <c>SavedSearchDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record SavedSearchDto(
    string Id,
    string Name,
    SearchCriteriaDto Criteria,
    AlertState AlertState,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);
