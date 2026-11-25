using System.ComponentModel.DataAnnotations;

namespace RoomToRentRadar.Shared.Dtos;

/// <summary>Request DTO cho POST /api/v1/favourites/toggle (US06 T06.1).</summary>
public sealed class ToggleFavouriteRequest
{
    [Required]
    public string ListingId { get; set; } = string.Empty;

    /// <summary>Hành động: "Add", "Remove" hoặc để trống / "Toggle" để tự đảo trạng thái.</summary>
    public string? Action { get; set; }
}

/// <summary>Thông tin một phòng yêu thích kèm giá và trạng thái sống mới nhất.</summary>
public sealed class FavouriteItemDto
{
    public string ListingId { get; set; } = string.Empty;

    public string CampusId { get; set; } = "ictu-main";

    public string Title { get; set; } = string.Empty;

    /// <summary>Giá thuê hiện tại của phòng.</summary>
    public decimal Rent { get; set; }

    /// <summary>Giá thuê tại thời điểm người dùng bấm yêu thích.</summary>
    public decimal SavedRent { get; set; }

    public bool HasPriceChanged => Rent != SavedRent;

    public double Area { get; set; }

    public double DistanceMeters { get; set; }

    public string RoomType { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string ThumbnailUrl { get; set; } = string.Empty;

    /// <summary>Trạng thái hiện tại của phòng (Available, Rented, Unavailable...).</summary>
    public string Status { get; set; } = "Available";

    public bool IsAvailable { get; set; }

    public bool IsVerified { get; set; }

    public DateTimeOffset SavedAt { get; set; }
}

/// <summary>Response của POST /api/v1/favourites/toggle và DELETE /api/v1/favourites/{listingId}.</summary>
public sealed class ToggleFavouriteResponse
{
    public string UserId { get; set; } = string.Empty;

    public string ListingId { get; set; } = string.Empty;

    public bool IsFavourited { get; set; }

    /// <summary>Added | Removed | NoChange.</summary>
    public string ActionApplied { get; set; } = string.Empty;

    public int TotalFavourites { get; set; }

    public long SyncVersion { get; set; }

    public DateTimeOffset SyncedAt { get; set; }

    public decimal? CurrentRent { get; set; }

    public string? CurrentStatus { get; set; }

    public FavouriteItemDto? Item { get; set; }
}

/// <summary>Response của GET /api/v1/favourites.</summary>
public sealed class FavouritesListResponse
{
    public string UserId { get; set; } = string.Empty;

    public int TotalCount { get; set; }

    public long SyncVersion { get; set; }

    public DateTimeOffset SyncedAt { get; set; }

    public List<FavouriteItemDto> Items { get; set; } = [];
}

/// <summary>Request đồng bộ danh sách yêu thích đa thiết bị (POST /api/v1/favourites/sync).</summary>
public sealed class SyncFavouritesRequest
{
    public List<string> ListingIds { get; set; } = [];

    public List<string> RemovedListingIds { get; set; } = [];

    public DateTimeOffset? ClientLastSyncedAt { get; set; }
}

/// <summary>Response sau khi đồng bộ danh sách yêu thích theo tài khoản.</summary>
public sealed class SyncFavouritesResponse
{
    public string UserId { get; set; } = string.Empty;

    public int TotalCount { get; set; }

    public int AddedCount { get; set; }

    public int RemovedCount { get; set; }

    public List<string> SkippedListingIds { get; set; } = [];

    public long SyncVersion { get; set; }

    public DateTimeOffset SyncedAt { get; set; }

    public List<FavouriteItemDto> Items { get; set; } = [];
}
