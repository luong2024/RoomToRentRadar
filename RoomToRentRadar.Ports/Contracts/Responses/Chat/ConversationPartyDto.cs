namespace RoomToRentRadar.Ports.Contracts.Responses.Chat;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>ConversationPartyDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record ConversationPartyDto(
    string Id,
    string DisplayName,
    UserRole Role
);
