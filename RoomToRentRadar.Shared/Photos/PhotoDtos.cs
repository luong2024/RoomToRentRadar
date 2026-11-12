namespace RoomToRentRadar.Shared.Photos;

/// <summary>Một ảnh của phòng (dùng chung cho Upload response và Gallery response).</summary>
public class PhotoDto
{
    public string Id { get; set; } = string.Empty;
    /// <summary>URL tương đối của ảnh đã nén, ví dụ /media/listings/room-001/abc.jpg (ghép với base URL của API).</summary>
    public string Url { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public bool Is360 { get; set; }
    public int Order { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long SizeBytes { get; set; }
}

/// <summary>Response của POST /api/v1/listings/{listingId}/photos.</summary>
public class UploadPhotosResponse
{
    public string ListingId { get; set; } = string.Empty;
    public int UploadedCount { get; set; }
    public int TotalPhotos { get; set; }
    public IReadOnlyList<PhotoDto> Photos { get; set; } = [];
}

/// <summary>Response của GET /api/v1/listings/{listingId}/photos.</summary>
public class PhotoGalleryResponse
{
    public string ListingId { get; set; } = string.Empty;
    public int TotalPhotos { get; set; }
    public int Total360Photos { get; set; }
    public IReadOnlyList<PhotoDto> Photos { get; set; } = [];
}
