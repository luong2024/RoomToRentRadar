using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.UseCases.RoomGallery;

internal static class RoomGalleryMapper
{
    public static RoomPhotoDto ToDto(RoomPhoto photo) =>
        new(
            Id: photo.Id,
            RoomId: photo.RoomId,
            Url: photo.Url,
            ThumbnailUrl: photo.ThumbnailUrl,
            Is360: photo.Is360,
            IsCover: photo.IsCover,
            DisplayOrder: photo.DisplayOrder,
            Width: photo.Width,
            Height: photo.Height,
            SizeBytes: photo.SizeBytes,
            ContentType: photo.ContentType,
            Caption: photo.Caption,
            UploadedAtUtc: photo.UploadedAtUtc);

    public static RoomGalleryResponse ToGalleryResponse(string roomId, IReadOnlyList<RoomPhoto> photos)
    {
        var orderedDtos = photos
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.UploadedAtUtc)
            .Select(ToDto)
            .ToList();

        return new RoomGalleryResponse(
            RoomId: roomId,
            TotalCount: orderedDtos.Count,
            HasPhotos: orderedDtos.Count > 0,
            Has360Photos: orderedDtos.Any(p => p.Is360),
            Photos: orderedDtos);
    }
}
