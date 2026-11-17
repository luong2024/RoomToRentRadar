using RoomToRentRadar.Shared.Notifications;

namespace RoomToRentRadar.Models;

public sealed record MapNotificationItem(NotificationDto Notification)
{
    public string Title => Notification.Title;

    public string Body => Notification.Body;

    public string TimeText => Notification.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}
