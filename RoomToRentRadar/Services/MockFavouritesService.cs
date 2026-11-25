using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Services;

/// <summary>
/// Lưu danh sách yêu thích in-memory dùng cho offline fallback và Unit Test (US06 T06.2).
/// </summary>
public sealed class MockFavouritesService : IFavouritesService
{
    private readonly List<RoomListingItemModel> _items =
    [
        new()
        {
            Id = "room-ictu-001",
            CampusId = "ictu-main",
            Title = "Studio Ban Công Thoáng Mát Cách Cổng Chính ICTU 400m",
            Rent = 1_800_000m,
            Area = 22.5,
            DistanceMeters = 400,
            RoomType = "Studio",
            Address = "Ngõ 112 Đường Z115, Quyết Thắng, TP. Thái Nguyên",
            ThumbnailUrl = "room_sample_1.png",
            Status = "Available",
            IsVerified = true,
            IsFavourite = true
        },
        new()
        {
            Id = "room-ictu-002",
            CampusId = "ictu-main",
            Title = "Phòng Khép Kín Full Nội Thất Gần Cổng Phụ ICTU",
            Rent = 2_100_000m,
            Area = 25,
            DistanceMeters = 520,
            RoomType = "Khép kín",
            Address = "Ngõ 68 Đường Z115, Tân Thịnh, TP. Thái Nguyên",
            ThumbnailUrl = "room_sample_2.png",
            Status = "Available",
            IsVerified = true,
            IsFavourite = true
        }
    ];

    public event EventHandler? FavouritesChanged;

    public Task<IReadOnlyList<RoomListingItemModel>> GetFavouritesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RoomListingItemModel> snapshot = _items.ToList();
        return Task.FromResult(snapshot);
    }

    public Task<bool> IsFavouriteAsync(string listingId, CancellationToken cancellationToken = default)
    {
        bool exists = _items.Any(r => string.Equals(r.Id, listingId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public async Task<bool> ToggleFavouriteAsync(RoomListingItemModel room, CancellationToken cancellationToken = default)
    {
        if (await IsFavouriteAsync(room.Id, cancellationToken))
        {
            await RemoveFavouriteAsync(room.Id, cancellationToken);
            room.IsFavourite = false;
            return false;
        }

        await AddFavouriteAsync(room, cancellationToken);
        room.IsFavourite = true;
        return true;
    }

    public Task AddFavouriteAsync(RoomListingItemModel room, CancellationToken cancellationToken = default)
    {
        if (!_items.Any(r => string.Equals(r.Id, room.Id, StringComparison.OrdinalIgnoreCase)))
        {
            room.IsFavourite = true;
            _items.Insert(0, room);
            FavouritesChanged?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }

    public Task RemoveFavouriteAsync(string listingId, CancellationToken cancellationToken = default)
    {
        int removed = _items.RemoveAll(r => string.Equals(r.Id, listingId, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            FavouritesChanged?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }
}