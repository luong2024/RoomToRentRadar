using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.SqliteDataAccess.Repositories;

public sealed class NotificationRepository(AppDbContext dbContext) : INotificationRepository
{
    public async Task<IReadOnlyList<Notification>> GetRecentAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken)
        => await dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public void Add(Notification notification) => dbContext.Notifications.Add(notification);
}
