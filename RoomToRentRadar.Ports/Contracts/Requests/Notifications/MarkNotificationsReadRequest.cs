namespace RoomToRentRadar.Ports.Contracts.Requests.Notifications;

/// <summary>Matches <c>MarkNotificationsReadRequest</c> in <c>docs/api/openapi.yaml</c> 1:1 (either ids or all = true).</summary>
public sealed record MarkNotificationsReadRequest(
    IReadOnlyList<string>? Ids,
    bool All
);
