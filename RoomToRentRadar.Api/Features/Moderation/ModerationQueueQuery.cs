namespace RoomToRentRadar.Api.Features.Moderation;

public sealed record ModerationQueueQuery(int Limit, string? Cursor);
