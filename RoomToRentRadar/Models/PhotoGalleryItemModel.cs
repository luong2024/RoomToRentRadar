using System;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Models;

public sealed class PhotoGalleryItemModel
{
    public string Id { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = "placeholder_room.png";
    public string ThumbnailUrl { get; init; } = "placeholder_room.png";
    public bool Is360 { get; init; }
    public int Order { get; init; }
    public string Caption { get; init; } = string.Empty;
    public string BadgeText => Is360 ? "360° VR" : $"Ảnh {Order + 1}";

    public static PhotoGalleryItemModel FromDto(GalleryPhotoDto dto, string apiBaseUrl)
    {
        var rawThumbnail = string.IsNullOrWhiteSpace(dto.ThumbnailUrl) ? dto.Url : dto.ThumbnailUrl;

        return new PhotoGalleryItemModel
        {
            Id = dto.Id,
            ImageUrl = ResolveUrl(dto.Url, apiBaseUrl),
            ThumbnailUrl = ResolveUrl(rawThumbnail, apiBaseUrl),
            Is360 = dto.Is360,
            Order = dto.Order,
            Caption = dto.Is360 ? "Ảnh toàn cảnh 360° — chạm để xoay" : $"Góc chụp #{dto.Order + 1}"
        };
    }

    public static string ResolveUrl(string rawUrl, string apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return "placeholder_room.png";

        if (rawUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || rawUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || !rawUrl.StartsWith('/'))
        {
            return rawUrl;
        }

        return $"{apiBaseUrl.TrimEnd('/')}{rawUrl}";
    }
}
