namespace RoomToRentRadar.Api.Models;

/// <summary>
/// Document tương thích container UsersInteractions của Azure Cosmos DB (Partition Key: /userId).
/// </summary>
public sealed class UserFavouritesDocument
{
    public string Id { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string DocumentType { get; set; } = "Favourites";

    public long SyncVersion { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<FavouriteRecord> Items { get; set; } = [];
}

public sealed class FavouriteRecord
{
    public string ListingId { get; set; } = string.Empty;

    public decimal SavedRent { get; set; }

    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
}
