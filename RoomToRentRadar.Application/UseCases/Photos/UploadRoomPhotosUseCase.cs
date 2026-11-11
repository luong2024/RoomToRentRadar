using RoomToRentRadar.Ports.Contracts.Requests.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Inbound.UseCases.Photos;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Photos;

public sealed class UploadRoomPhotosUseCase : IUploadRoomPhotosUseCase
{
    private readonly IRoomPhotoRepository _repository;
    private readonly IPhotoStoragePort _storage;

    public UploadRoomPhotosUseCase(IRoomPhotoRepository repository, IPhotoStoragePort storage)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(storage);

        _repository = repository;
        _storage = storage;
    }

    public Task<IReadOnlyList<RoomPhotoDto>> ExecuteAsync(UploadRoomPhotosRequest request, CancellationToken ct = default)
        => throw new NotImplementedException();
}
