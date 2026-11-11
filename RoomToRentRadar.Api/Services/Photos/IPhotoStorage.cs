namespace RoomToRentRadar.Api.Services.Photos;

/// <summary>
/// Trừu tượng hóa nơi lưu tệp. Hiện có <see cref="LocalPhotoStorage"/>;
/// sau này chỉ cần viết thêm AzureBlobPhotoStorage và đổi DI là chuyển sang cloud.
/// </summary>
public interface IPhotoStorage
{
    /// <summary>Lưu tệp, trả về đường dẫn tương đối (lưu vào DB).</summary>
    Task<string> SaveAsync(string roomId, string fileName, byte[] content, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>Đổi đường dẫn tương đối thành URL công khai.</summary>
    string GetPublicUrl(string storagePath, string baseUrl);
}
