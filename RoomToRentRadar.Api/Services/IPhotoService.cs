using RoomToRentRadar.Api.DTOs;

namespace RoomToRentRadar.Api.Services;

public interface IPhotoService
{
    /// <exception cref="Photos.RoomNotFoundException"/>
    /// <exception cref="Photos.PhotoValidationException"/>
    Task<UploadPhotosResponse> UploadAsync(string roomId, UploadPhotosRequest request, string baseUrl, CancellationToken cancellationToken = default);

    /// <exception cref="Photos.RoomNotFoundException"/>
    Task<PhotoGalleryResponse> GetGalleryAsync(string roomId, string baseUrl, CancellationToken cancellationToken = default);
}
