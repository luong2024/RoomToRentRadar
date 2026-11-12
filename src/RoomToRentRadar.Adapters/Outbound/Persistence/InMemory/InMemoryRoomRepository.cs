using System.Collections.Concurrent;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.InMemory;

public class InMemoryRoomRepository : IRoomRepository
{
    private readonly ConcurrentDictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryRoomRepository()
    {
    }

    public InMemoryRoomRepository(IEnumerable<Room> initialRooms)
    {
        foreach (var room in initialRooms)
        {
            _rooms[room.Id] = room;
        }
    }

    public void Upsert(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);
        _rooms[room.Id] = room;
    }

    public Task<Room?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Task.FromResult<Room?>(null);
        }

        _rooms.TryGetValue(id, out var room);
        return Task.FromResult(room);
    }

    public Task<IReadOnlyList<Room>> GetSimilarRoomsAsync(string roomId, decimal price, double area, CancellationToken cancellationToken = default)
    {
        var minPrice = price * 0.7m;
        var maxPrice = price * 1.3m;

        var similar = _rooms.Values
            .Where(r => !string.Equals(r.Id, roomId, StringComparison.OrdinalIgnoreCase) &&
                        r.Status == RoomStatus.Available &&
                        r.Price >= minPrice && r.Price <= maxPrice)
            .Take(5)
            .ToList();

        return Task.FromResult<IReadOnlyList<Room>>(similar);
    }
}
