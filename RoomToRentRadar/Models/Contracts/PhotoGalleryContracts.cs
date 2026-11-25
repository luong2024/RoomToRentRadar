using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RoomToRentRadar.Models.Contracts;

/// <summary>
/// Khớp DTO của Backend T03.1 (GET /api/v1/listings/{listingId}/photos).
/// </summary>
public sealed class GalleryPhotoDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("thumbnailUrl")]
    public string ThumbnailUrl { get; set; } = string.Empty;

    [JsonPropertyName("is360")]
    public bool Is360 { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }
}

public sealed class PhotoGalleryResponse
{
    [JsonPropertyName("listingId")]
    public string ListingId { get; set; } = string.Empty;

    [JsonPropertyName("totalPhotos")]
    public int TotalPhotos { get; set; }

    [JsonPropertyName("total360Photos")]
    public int Total360Photos { get; set; }

    [JsonPropertyName("photos")]
    public List<GalleryPhotoDto> Photos { get; set; } = [];
}
