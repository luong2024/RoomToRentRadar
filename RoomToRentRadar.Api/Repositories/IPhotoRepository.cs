using RoomToRentRadar.Api.Models;

namespace RoomToRentRadar.Api.Repositories;

public interface IPhotoRepository
{
    Task AddRangeAsync(IEnumerable<RoomPhoto> photos, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoomPhoto>> GetByRoomAsync(string roomId, CancellationToken cancellationToken = default);
}
