namespace RoomToRentRadar.Ports.Contracts.Requests.Chat;

/// <summary>Matches <c>SendMessageRequest</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record SendMessageRequest(
    string ClientMessageId,
    string Body
);
