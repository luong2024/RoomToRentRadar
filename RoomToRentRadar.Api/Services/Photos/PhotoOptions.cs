namespace RoomToRentRadar.Api.Services.Photos;

/// <summary>Cấu hình section "Photos" trong appsettings.json.</summary>
public class PhotoOptions
{
    public const string SectionName = "Photos";

    /// <summary>Thư mục lưu ảnh (tương đối so với content root nếu không phải đường dẫn tuyệt đối).</summary>
    public string StoragePath { get; set; } = "App_Data/uploads";
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;   // <= 10 MB / file (US08 AC4)
    public int MaxFilesPerRequest { get; set; } = 10;
    public int MaxPhotosPerListing { get; set; } = 30;
    public int JpegQuality { get; set; } = 80;
    public int MaxImageWidth { get; set; } = 1920;
    public int Max360Width { get; set; } = 4096;                     // ảnh equirectangular 2:1
    public int ThumbnailWidth { get; set; } = 400;
}
