namespace RoomToRentRadar.Ports.Models.Moderation;

public sealed record ModerationQueueQuery(int Limit, string? Cursor);
