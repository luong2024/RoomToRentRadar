namespace RoomToRentRadar.Ports.Contracts.Responses.Chat;

/// <summary>Matches <c>MessageDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record MessageDto(
    string Id,
    string ConversationId,
    string SenderId,
    string ClientMessageId,
    string Body,
    DateTime SentAtUtc
);
