using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Shared.Notifications;

namespace RoomToRentRadar.UseCases.Notifications;

public sealed class NotificationsService(INotificationRepository notificationRepository)
    : INotificationsUseCase
{
    public async Task<IReadOnlyList<NotificationDto>> GetRecentAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken)
    {
        var notifications = await notificationRepository.GetRecentAsync(
            userId,
            limit,
            cancellationToken);

        return notifications
            .Select(notification => new NotificationDto(
                notification.Id,
                notification.Title,
                notification.Body,
                notification.ListingId,
                notification.CreatedAt))
            .ToList();
    }
}
