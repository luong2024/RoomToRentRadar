namespace RoomToRentRadar.Ports.Contracts.Requests.SavedSearches;

using RoomToRentRadar.Ports.Contracts.Requests.Rooms;

/// <summary>Matches <c>UpdateSavedSearchRequest</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record UpdateSavedSearchRequest(
    string Name,
    SearchCriteriaDto Criteria
);
