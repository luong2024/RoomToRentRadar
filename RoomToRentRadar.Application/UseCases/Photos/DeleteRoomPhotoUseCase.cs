using RoomToRentRadar.Ports.Inbound.UseCases.Photos;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Photos;

public sealed class DeleteRoomPhotoUseCase : IDeleteRoomPhotoUseCase
{
    private readonly IRoomPhotoRepository _repository;
    private readonly IPhotoStoragePort _storage;

    public DeleteRoomPhotoUseCase(IRoomPhotoRepository repository, IPhotoStoragePort storage)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(storage);

        _repository = repository;
        _storage = storage;
    }

    public Task<bool> ExecuteAsync(string roomId, string photoId, CancellationToken ct = default)
        => throw new NotImplementedException();
}
