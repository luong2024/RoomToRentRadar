using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Shared.Notifications;

namespace RoomToRentRadar.Api.Features.Notifications;

public sealed class NotificationsService(AppDbContext dbContext)
{
    public async Task<IReadOnlyList<NotificationDto>> GetRecentAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken)
    {
        var notifications = await dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

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
