using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.Ports.Outbound;

public interface IRoomPhotoRepository
{
    Task<IReadOnlyList<RoomPhoto>> GetByRoomIdAsync(string roomId, CancellationToken cancellationToken = default);

    Task<RoomPhoto?> GetByIdAsync(string roomId, string photoId, CancellationToken cancellationToken = default);

    Task<int> CountByRoomIdAsync(string roomId, CancellationToken cancellationToken = default);

    Task AddAsync(RoomPhoto photo, CancellationToken cancellationToken = default);

    Task<RoomPhoto> AddWithinQuotaAsync(
        string roomId,
        int maxPhotosPerRoom,
        bool requestCover,
        Func<int, bool, RoomPhoto> photoFactory,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(RoomPhoto photo, CancellationToken cancellationToken = default);

    Task DeleteAsync(string roomId, string photoId, CancellationToken cancellationToken = default);
}
