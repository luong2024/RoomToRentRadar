using RoomToRentRadar.Shared.Notifications;

namespace RoomToRentRadar.Ports.Inbound;

public interface INotificationsUseCase
{
    Task<IReadOnlyList<NotificationDto>> GetRecentAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken);
}
