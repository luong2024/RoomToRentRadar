using RoomToRentRadar.Ports.Contracts.Requests.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Photos;

public interface IUploadRoomPhotosUseCase
{
    Task<IReadOnlyList<RoomPhotoDto>> ExecuteAsync(UploadRoomPhotosRequest request, CancellationToken ct = default);
}
