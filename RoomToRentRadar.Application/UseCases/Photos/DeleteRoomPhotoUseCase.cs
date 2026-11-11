using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.Services;
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

    public async Task<bool> ExecuteAsync(string roomId, string photoId, CancellationToken ct = default)
    {
        if (!PhotoFileRules.IsValidRoomId(roomId))
        {
            throw new DomainValidationException("Room id is invalid.");
        }

        var photo = await _repository.GetAsync(roomId, photoId, ct);
        if (photo is null)
        {
            return false;
        }

        await _storage.DeleteAsync(roomId, photo.FileName, ct);
        await _repository.DeleteAsync(photoId, ct);

        return true;
    }
}
