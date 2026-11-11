using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Infrastructure.Adapters.Mocks;

public sealed class InMemoryRoomPhotoRepository : IRoomPhotoRepository
{
    private readonly List<RoomPhoto> _photos = new();
    private readonly object _lock = new();

    public Task AddAsync(RoomPhoto photo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(photo);
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            _photos.Add(photo);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RoomPhoto>> ListByRoomAsync(string roomId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            IReadOnlyList<RoomPhoto> photos = _photos
                .Where(p => string.Equals(p.RoomId, roomId, StringComparison.Ordinal))
                .OrderBy(p => p.UploadedAtUtc)
                .ToList();

            return Task.FromResult(photos);
        }
    }

    public Task<RoomPhoto?> GetAsync(string roomId, string photoId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(photoId);
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            var photo = _photos.FirstOrDefault(p =>
                string.Equals(p.RoomId, roomId, StringComparison.Ordinal) &&
                string.Equals(p.Id, photoId, StringComparison.Ordinal));

            return Task.FromResult(photo);
        }
    }

    public Task DeleteAsync(string photoId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(photoId);
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            _photos.RemoveAll(p => string.Equals(p.Id, photoId, StringComparison.Ordinal));
        }

        return Task.CompletedTask;
    }
}
