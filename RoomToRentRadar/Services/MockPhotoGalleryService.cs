using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Services;

/// <summary>
/// Mock >= 8 ảnh (AC1), hỗ trợ 360° (AC2), và trả danh sách rỗng cho "room-empty" để kiểm thử placeholder (AC4).
/// </summary>
public sealed class MockPhotoGalleryService : IPhotoGalleryService
{
    public Task<IReadOnlyList<PhotoGalleryItemModel>> GetPhotosAsync(
        string listingId,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(listingId, "room-empty", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<IReadOnlyList<PhotoGalleryItemModel>>([]);
        }

        IReadOnlyList<PhotoGalleryItemModel> photos =
        [
            new() { Id = "p1", Order = 0, Is360 = true,  ImageUrl = "room_sample_1.png", ThumbnailUrl = "room_sample_1.png", Caption = "Toàn cảnh 360° phòng chính" },
            new() { Id = "p2", Order = 1, Is360 = false, ImageUrl = "room_sample_2.png", ThumbnailUrl = "room_sample_2.png", Caption = "Khu vực giường & bàn học" },
            new() { Id = "p3", Order = 2, Is360 = false, ImageUrl = "room_sample_3.png", ThumbnailUrl = "room_sample_3.png", Caption = "Ban công thoáng đón nắng" },
            new() { Id = "p4", Order = 3, Is360 = false, ImageUrl = "room_sample_4.png", ThumbnailUrl = "room_sample_4.png", Caption = "Kệ bếp nấu ăn riêng" },
            new() { Id = "p5", Order = 4, Is360 = false, ImageUrl = "room_sample_1.png", ThumbnailUrl = "room_sample_1.png", Caption = "Cửa sổ & rèm chống nắng" },
            new() { Id = "p6", Order = 5, Is360 = false, ImageUrl = "room_sample_2.png", ThumbnailUrl = "room_sample_2.png", Caption = "Tủ quần áo & điều hòa" },
            new() { Id = "p7", Order = 6, Is360 = false, ImageUrl = "room_sample_3.png", ThumbnailUrl = "room_sample_3.png", Caption = "Nhà vệ sinh khép kín" },
            new() { Id = "p8", Order = 7, Is360 = true,  ImageUrl = "room_sample_4.png", ThumbnailUrl = "room_sample_4.png", Caption = "Toàn cảnh 360° sân để xe & hành lang" }
        ];

        return Task.FromResult(photos);
    }
}
