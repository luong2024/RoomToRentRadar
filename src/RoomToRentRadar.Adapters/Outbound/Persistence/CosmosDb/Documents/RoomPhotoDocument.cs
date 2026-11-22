using System.Text.Json.Serialization;
using Newtonsoft.Json;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

public sealed class RoomPhotoDocument
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("roomId")]
    [JsonPropertyName("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [JsonProperty("url")]
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonProperty("thumbnailUrl")]
    [JsonPropertyName("thumbnailUrl")]
    public string ThumbnailUrl { get; set; } = string.Empty;

    [JsonProperty("objectKey")]
    [JsonPropertyName("objectKey")]
    public string ObjectKey { get; set; } = string.Empty;

    [JsonProperty("thumbnailObjectKey")]
    [JsonPropertyName("thumbnailObjectKey")]
    public string ThumbnailObjectKey { get; set; } = string.Empty;

    [JsonProperty("contentType")]
    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = "image/jpeg";

    [JsonProperty("sizeBytes")]
    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonProperty("width")]
    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonProperty("height")]
    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonProperty("is360")]
    [JsonPropertyName("is360")]
    public bool Is360 { get; set; }

    [JsonProperty("isCover")]
    [JsonPropertyName("isCover")]
    public bool IsCover { get; set; }

    [JsonProperty("displayOrder")]
    [JsonPropertyName("displayOrder")]
    public int DisplayOrder { get; set; }

    [JsonProperty("caption")]
    [JsonPropertyName("caption")]
    public string? Caption { get; set; }

    [JsonProperty("uploadedByUserId")]
    [JsonPropertyName("uploadedByUserId")]
    public string UploadedByUserId { get; set; } = string.Empty;

    [JsonProperty("uploadedAtUtc")]
    [JsonPropertyName("uploadedAtUtc")]
    public DateTimeOffset UploadedAtUtc { get; set; }

    [JsonProperty("isDeleted")]
    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }

    public static RoomPhotoDocument FromDomain(RoomPhoto photo)
    {
        ArgumentNullException.ThrowIfNull(photo);

        return new RoomPhotoDocument
        {
            Id = photo.Id,
            RoomId = photo.RoomId,
            Url = photo.Url,
            ThumbnailUrl = photo.ThumbnailUrl,
            ObjectKey = photo.ObjectKey,
            ThumbnailObjectKey = photo.ThumbnailObjectKey,
            ContentType = photo.ContentType,
            SizeBytes = photo.SizeBytes,
            Width = photo.Width,
            Height = photo.Height,
            Is360 = photo.Is360,
            IsCover = photo.IsCover,
            DisplayOrder = photo.DisplayOrder,
            Caption = photo.Caption,
            UploadedByUserId = photo.UploadedByUserId.ToString("D"),
            UploadedAtUtc = photo.UploadedAtUtc,
            IsDeleted = false
        };
    }

    public RoomPhoto ToDomain()
    {
        _ = Guid.TryParse(UploadedByUserId, out var uploaderGuid);

        return RoomPhoto.Reconstitute(
            id: Id,
            roomId: RoomId,
            url: Url,
            thumbnailUrl: ThumbnailUrl,
            objectKey: ObjectKey,
            thumbnailObjectKey: ThumbnailObjectKey,
            contentType: ContentType,
            sizeBytes: SizeBytes,
            width: Width,
            height: Height,
            is360: Is360,
            isCover: IsCover,
            displayOrder: DisplayOrder,
            caption: Caption,
            uploadedByUserId: uploaderGuid,
            uploadedAtUtc: UploadedAtUtc);
    }
}
