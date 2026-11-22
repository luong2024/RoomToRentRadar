using RoomToRentRadar.Application.DTOs;

namespace RoomToRentRadar.Application.Ports.Inbound;

public interface IGetRoomGalleryUseCase
{
    Task<RoomGalleryResponse> ExecuteAsync(string roomId, CancellationToken cancellationToken = default);
}

public interface IUploadRoomPhotoUseCase
{
    Task<RoomPhotoDto> ExecuteAsync(UploadRoomPhotoCommand command, CancellationToken cancellationToken = default);
}

public interface IDeleteRoomPhotoUseCase
{
    Task ExecuteAsync(string roomId, string photoId, CancellationToken cancellationToken = default);
}

public interface IReorderRoomPhotosUseCase
{
    Task<RoomGalleryResponse> ExecuteAsync(ReorderRoomPhotosCommand command, CancellationToken cancellationToken = default);
}
