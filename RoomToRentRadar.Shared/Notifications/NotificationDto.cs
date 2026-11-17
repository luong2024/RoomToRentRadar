namespace RoomToRentRadar.Shared.Notifications;

public sealed record NotificationDto(
    Guid Id,
    string Title,
    string Body,
    Guid? ListingId,
    DateTime CreatedAt);
