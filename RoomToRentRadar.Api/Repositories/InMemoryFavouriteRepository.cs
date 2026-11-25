using System.Collections.Concurrent;
using RoomToRentRadar.Api.Models;

namespace RoomToRentRadar.Api.Repositories;

public sealed class InMemoryFavouriteRepository : IFavouriteRepository
{
    private readonly ConcurrentDictionary<string, UserFavouritesDocument> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task<UserFavouritesDocument?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(userId, out var doc);
        return Task.FromResult(doc is null ? null : Clone(doc));
    }

    public Task<UserFavouritesDocument> SaveAsync(UserFavouritesDocument document, CancellationToken cancellationToken = default)
    {
        var copy = Clone(document);
        _store[copy.UserId] = copy;
        return Task.FromResult(Clone(copy));
    }

    private static UserFavouritesDocument Clone(UserFavouritesDocument source) => new()
    {
        Id = source.Id,
        UserId = source.UserId,
        DocumentType = source.DocumentType,
        SyncVersion = source.SyncVersion,
        UpdatedAt = source.UpdatedAt,
        Items = source.Items
            .Select(item => new FavouriteRecord
            {
                ListingId = item.ListingId,
                SavedRent = item.SavedRent,
                SavedAt = item.SavedAt
            })
            .ToList()
    };
}
