namespace RoomToRentRadar.Ports.Contracts.Responses.Chat;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>ConversationRoomDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record ConversationRoomDto(
    string Id,
    string Title,
    string? CoverThumbUrl,
    long MonthlyRentVnd,
    ListingStatus Status
);
