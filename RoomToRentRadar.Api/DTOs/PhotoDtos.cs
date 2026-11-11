using System.ComponentModel.DataAnnotations;

namespace RoomToRentRadar.Api.DTOs;

/// <summary>Request DTO (multipart/form-data) cho POST /api/rooms/{roomId}/photos.</summary>
public class UploadPhotosRequest
{
    /// <summary>Một hoặc nhiều tệp ảnh (JPEG/PNG), field name: "files".</summary>
    [Required]
    public List<IFormFile> Files { get; set; } = [];

    /// <summary>true nếu là ảnh 360° (equirectangular, tỉ lệ ~2:1). Ảnh 360 được giữ độ phân giải cao hơn.</summary>
    public bool Is360 { get; set; }
}

/// <summary>Thông tin một ảnh trong gallery.</summary>
public record PhotoDto(
    string Id,
    string RoomId,
    string Url,
    bool Is360,
    int Width,
    int Height,
    long SizeBytes,
    DateTime UploadedAt);

/// <summary>Response DTO của API upload.</summary>
public record UploadPhotosResponse(string RoomId, int UploadedCount, IReadOnlyList<PhotoDto> Photos);

/// <summary>Response DTO của Gallery API.</summary>
public record PhotoGalleryResponse(string RoomId, int TotalPhotos, IReadOnlyList<PhotoDto> Photos);
