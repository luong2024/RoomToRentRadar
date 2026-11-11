namespace RoomToRentRadar.Api.Options;

public class PhotoOptions
{
    public const string SectionName = "Photos";

    /// <summary>Dung lượng tối đa mỗi tệp tải lên (byte). Mặc định 10 MB.</summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Số ảnh tối đa mỗi lần upload.</summary>
    public int MaxFilesPerRequest { get; set; } = 10;

    /// <summary>Cạnh dài tối đa của ảnh thường (px).</summary>
    public int MaxDimension { get; set; } = 1920;

    /// <summary>Cạnh dài tối đa của ảnh 360° (px) - cần cao hơn để xem panorama còn nét.</summary>
    public int Max360Dimension { get; set; } = 4096;

    /// <summary>Chất lượng JPEG sau nén (1-100).</summary>
    public int JpegQuality { get; set; } = 80;

    /// <summary>Thư mục gốc lưu ảnh (tương đối so với ContentRoot nếu không phải đường dẫn tuyệt đối).</summary>
    public string StorageRoot { get; set; } = "wwwroot/uploads";

    /// <summary>URL công khai tương ứng với StorageRoot.</summary>
    public string RequestPath { get; set; } = "/uploads";
}
