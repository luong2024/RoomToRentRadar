using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Ports.Outbound.Repositories;

public interface IRoomPhotoRepository
{
    Task AddAsync(RoomPhoto photo, CancellationToken ct = default);
    Task<IReadOnlyList<RoomPhoto>> ListByRoomAsync(string roomId, CancellationToken ct = default); // UploadedAtUtc ASC
    Task<RoomPhoto?> GetAsync(string roomId, string photoId, CancellationToken ct = default);
    Task DeleteAsync(string photoId, CancellationToken ct = default);
}
