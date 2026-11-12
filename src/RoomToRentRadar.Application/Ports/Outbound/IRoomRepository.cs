using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.Ports.Outbound;

public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Room>> GetSimilarRoomsAsync(string roomId, decimal price, double area, CancellationToken cancellationToken = default);
}
