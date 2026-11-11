using System.Collections.Concurrent;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Infrastructure.Adapters.Mocks;

public sealed class InMemoryRoomRepository : IRoomRepository
{
    private readonly ConcurrentDictionary<string, Room> _roomsById = new(StringComparer.Ordinal);

    public InMemoryRoomRepository()
    {
        foreach (var room in RoomSeedData.CreateRooms())
        {
            _roomsById[room.Id] = room;
        }
    }

    public Task<IReadOnlyList<Room>> GetAllAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<Room> snapshot = _roomsById.Values.Select(Clone).ToList();
        return Task.FromResult(snapshot);
    }

    public Task<Room?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_roomsById.TryGetValue(id, out var room) ? Clone(room) : null);
    }

    public Task AddAsync(Room room, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(room);
        ct.ThrowIfCancellationRequested();
        _roomsById[room.Id] = Clone(room);
        return Task.CompletedTask;
    }

    private static Room Clone(Room source) => new()
    {
        Id = source.Id,
        Title = source.Title,
        Description = source.Description,
        Price = source.Price,
        Area = source.Area,
        Address = source.Address,
        Latitude = source.Latitude,
        Longitude = source.Longitude,
        LandlordId = source.LandlordId,
        IsAvailable = source.IsAvailable,
        IsVerified = source.IsVerified,
        CreatedAtUtc = source.CreatedAtUtc
    };
}
