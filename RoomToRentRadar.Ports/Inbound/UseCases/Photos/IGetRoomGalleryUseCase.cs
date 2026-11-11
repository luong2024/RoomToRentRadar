using RoomToRentRadar.Ports.Contracts.Responses.Photos;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Photos;

public interface IGetRoomGalleryUseCase
{
    Task<RoomGalleryDto> ExecuteAsync(string roomId, CancellationToken ct = default);
}
