namespace RoomToRentRadar.Ports.Contracts.Responses.Chat;

/// <summary>Matches <c>MessagePage</c> in <c>docs/api/openapi.yaml</c> 1:1 (cursor paging, not PageMeta).</summary>
public sealed record MessagePage(
    IReadOnlyList<MessageDto> Items,
    string? NextBefore
);
