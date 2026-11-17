namespace RoomToRentRadar.Ports.Contracts.Requests.SavedSearches;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>SetAlertStateRequest</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record SetAlertStateRequest(
    AlertState AlertState
);
