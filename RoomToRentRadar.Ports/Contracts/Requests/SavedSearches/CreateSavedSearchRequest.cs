namespace RoomToRentRadar.Ports.Contracts.Requests.SavedSearches;

using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;

/// <summary>Matches <c>CreateSavedSearchRequest</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record CreateSavedSearchRequest(
    string Name,
    SearchCriteriaDto Criteria,
    AlertState AlertState
);
