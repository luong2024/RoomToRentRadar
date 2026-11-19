namespace RoomToRentRadar.Domain.Enums;

/// <summary>
/// Admin moderation queues, matching the inline <c>queue</c> query parameter enum of <c>listModerationRooms</c>
/// in <c>docs/api/openapi.yaml</c>; wire values are lowercase, so gateways send <c>ToString().ToLowerInvariant()</c>.
/// </summary>
public enum ModerationQueue
{
    Pending = 1,
    Rereview = 2
}