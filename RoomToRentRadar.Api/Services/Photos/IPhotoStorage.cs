namespace RoomToRentRadar.Api.Services.Photos;

/// <summary>
/// Trừu tượng hoá nơi lưu tệp. Hiện có LocalPhotoStorage (thư mục server);
/// sau này chỉ cần thêm AzureBlobPhotoStorage và đổi đăng ký DI.
/// </summary>
public interface IPhotoStorage
{
    /// <summary>Lưu tệp, trả về URL công khai (tương đối).</summary>
    Task<string> SaveAsync(string listingId, string fileName, byte[] content, CancellationToken ct);

    Task DeleteAsync(string listingId, string fileName, CancellationToken ct);
}
