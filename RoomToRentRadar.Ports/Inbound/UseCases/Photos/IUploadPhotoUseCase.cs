using RoomToRentRadar.Ports.Contracts.Requests.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Photos;

public interface IUploadPhotoUseCase
{
    Task<PhotoUploadDto> ExecuteAsync(UploadPhotoRequest request, CancellationToken ct = default);
}
