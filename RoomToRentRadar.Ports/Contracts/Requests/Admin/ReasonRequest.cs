namespace RoomToRentRadar.Ports.Contracts.Requests.Admin;

/// <summary>Matches <c>ReasonRequest</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record ReasonRequest(
    string Reason
);