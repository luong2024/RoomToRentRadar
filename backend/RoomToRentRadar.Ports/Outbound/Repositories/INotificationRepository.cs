using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface INotificationRepository
{
    Task<IReadOnlyList<Notification>> GetRecentAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken);

    void Add(Notification notification);
}
