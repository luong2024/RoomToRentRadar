namespace RoomToRentRadar.Api.Services.Photos;

/// <summary>Kết quả sau khi kiểm tra và nén ảnh.</summary>
public sealed record ProcessedImage(byte[] Content, int Width, int Height, string ContentType, string Extension);

public interface IImageProcessor
{
    /// <summary>
    /// Kiểm tra ảnh hợp lệ (đọc nội dung thật, không tin đuôi file) rồi resize + nén.
    /// </summary>
    /// <exception cref="PhotoValidationException">Tệp không phải JPEG/PNG hợp lệ.</exception>
    Task<ProcessedImage> ProcessAsync(Stream input, bool is360, CancellationToken cancellationToken = default);
}
