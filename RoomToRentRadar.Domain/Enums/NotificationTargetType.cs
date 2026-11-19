namespace RoomToRentRadar.Domain.Enums;

/// <summary>
/// Entity a notification points at, matching the inline <c>targetType</c> enum of <c>NotificationDto</c>
/// in <c>docs/api/openapi.yaml</c>; a wire value of <c>null</c> means no target and has no enum member.
/// </summary>
public enum NotificationTargetType
{
    Room = 1,
    Conversation = 2,
    SavedSearch = 3
}