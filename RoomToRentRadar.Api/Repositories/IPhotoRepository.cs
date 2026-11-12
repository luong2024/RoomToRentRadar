using RoomToRentRadar.Shared.Photos;

namespace RoomToRentRadar.Api.Repositories;

public record PhotoAddResult(IReadOnlyList<PhotoDto> Added, int TotalPhotos);

public interface IPhotoRepository
{
    /// <summary>Danh sách ảnh theo Order; null nếu phòng không tồn tại.</summary>
    Task<IReadOnlyList<PhotoDto>?> GetPhotosAsync(string listingId, CancellationToken ct);

    /// <summary>Thêm ảnh (repository gán Order tiếp theo); null nếu phòng không tồn tại.</summary>
    Task<PhotoAddResult?> AddPhotosAsync(string listingId, IReadOnlyList<PhotoDto> photos, CancellationToken ct);
}
