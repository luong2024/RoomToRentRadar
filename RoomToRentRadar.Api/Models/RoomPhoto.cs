namespace RoomToRentRadar.Api.Models;

/// <summary>
/// Metadata ảnh lưu trong Cosmos DB (container "Photos", partition key "/roomId").
/// Tệp ảnh nằm ở storage; DB chỉ giữ đường dẫn tương đối (<see cref="StoragePath"/>)
/// để dễ chuyển sang Azure Blob Storage.
/// </summary>
public class RoomPhoto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Partition key.</summary>
    public string RoomId { get; set; } = string.Empty;

    /// <summary>Đường dẫn tương đối trong storage, ví dụ "rooms/{roomId}/{id}.jpg".</summary>
    public string StoragePath { get; set; } = string.Empty;

    public bool Is360 { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    /// <summary>Kích thước sau khi nén (byte).</summary>
    public long SizeBytes { get; set; }

    public string ContentType { get; set; } = "image/jpeg";

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
