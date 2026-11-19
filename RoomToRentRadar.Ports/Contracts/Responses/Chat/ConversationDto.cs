namespace RoomToRentRadar.Ports.Contracts.Responses.Chat;

/// <summary>Matches <c>ConversationDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record ConversationDto(
    string Id,
    ConversationRoomDto Room,
    ConversationPartyDto OtherParty,
    string? LastMessagePreview,
    DateTime? LastMessageAtUtc,
    int UnreadCount,
    bool CanSend,
    DateTime CreatedAtUtc
);
