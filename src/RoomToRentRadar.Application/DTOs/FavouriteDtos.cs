namespace RoomToRentRadar.Application.DTOs;

/// <summary>
/// Represents a single favourite room item returned to the client (US06 / T06.1).
/// IsAvailable is strictly fail-closed (defaults to false).
/// </summary>
public sealed class FavouriteRoomItemDto
{
    public string RoomId { get; init; } = string.Empty;

    public string Id { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public string FormattedPrice { get; init; } = string.Empty;

    public decimal? Deposit { get; init; }

    public string? FormattedDeposit { get; init; }

    public double Area { get; init; }

    public double AreaSqm { get; init; }

    public string FormattedArea { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public string RoomType { get; init; } = string.Empty;

    public int? Bedrooms { get; init; }

    public int? Bathrooms { get; init; }

    public IReadOnlyList<string> Amenities { get; init; } = [];

    public IReadOnlyList<string> ImageUrls { get; init; } = [];

    public string? ThumbnailUrl { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public bool IsVerified { get; init; }

    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Fail-closed availability flag. True only when the room exists, is Verified, and Status is Available.
    /// </summary>
    public bool IsAvailable { get; init; }

    public bool IsDeleted { get; init; }

    public string? UnavailableMessage { get; init; }

    public DateTime? LastUpdatedDate { get; init; }

    public DateTimeOffset FavouritedAtUtc { get; init; }
}

/// <summary>
/// Result of querying a user's full list of favourite rooms.
/// </summary>
public sealed record FavouriteListResultDto(
    IReadOnlyList<FavouriteRoomItemDto> Items,
    int TotalCount);

/// <summary>
/// Result of querying a user's favourite room IDs.
/// </summary>
public sealed record FavouriteIdsResultDto(
    IReadOnlyList<string> RoomIds,
    int TotalCount);

/// <summary>
/// Result of merging/synchronizing local favourite room IDs into the user's account.
/// </summary>
public sealed record SyncFavouritesResultDto(
    int AddedCount,
    IReadOnlyList<string> SkippedRoomIds,
    IReadOnlyList<FavouriteRoomItemDto> Favourites,
    int TotalCount);
