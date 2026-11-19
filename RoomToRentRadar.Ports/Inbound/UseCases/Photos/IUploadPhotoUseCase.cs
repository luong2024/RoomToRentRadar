using RoomToRentRadar.Ports.Contracts.Requests.Photos;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;

namespace RoomToRentRadar.Ports.Inbound.UseCases.Photos;

/// <summary>Validates and stores one listing photo (<c>operationId: uploadPhoto</c>, UC-30, BR-MED-02).</summary>
public interface IUploadPhotoUseCase
{
    Task<PhotoUploadDto> ExecuteAsync(UploadPhotoRequest request, CancellationToken ct = default);
}
