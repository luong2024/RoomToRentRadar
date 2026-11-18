namespace RoomToRentRadar.Ports.Contracts.Responses.Notifications;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>NotificationDto</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record NotificationDto(
    string Id,
    NotificationType Type,
    string Title,
    string Body,
    NotificationTargetType? TargetType,
    string? TargetId,
    bool IsRead,
    DateTime CreatedAtUtc
);
