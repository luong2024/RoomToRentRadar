using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Inbound.UseCases.Photos;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Photos;

public sealed class GetRoomGalleryUseCase : IGetRoomGalleryUseCase
{
    private readonly IRoomPhotoRepository _repository;

    public GetRoomGalleryUseCase(IRoomPhotoRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public Task<RoomGalleryDto> ExecuteAsync(string roomId, CancellationToken ct = default)
        => throw new NotImplementedException();
}
