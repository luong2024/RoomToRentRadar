using RoomToRentRadar.Api.Models;

namespace RoomToRentRadar.Api.Repositories;

/// <summary>
/// Phía ghi của container Listings cho chủ trọ đăng tin mới (US08 / T08.2).
/// Đọc danh sách tin của chủ trọ / đổi trạng thái thuộc T08.3 (<c>IListingModerationService</c>).
/// </summary>
public interface ILandlordListingRepository
{
    /// <exception cref="InvalidOperationException">Id đã tồn tại.</exception>
    Task AddAsync(ListingDocument listing, CancellationToken cancellationToken = default);

    /// <summary>Xóa cứng — chỉ dùng để rollback khi upload ảnh lúc tạo tin thất bại.</summary>
    Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default);
}
