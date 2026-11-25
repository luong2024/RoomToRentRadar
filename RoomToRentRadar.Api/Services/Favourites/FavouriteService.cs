using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services.Favourites;

public sealed class FavouriteService(
    IFavouriteRepository favouriteRepository,
    IListingRepository listingRepository) : IFavouriteService
{
    // Khóa theo từng user trên cùng một repository để tránh race condition giữa các request đồng thời.
    private static readonly ConditionalWeakTable<IFavouriteRepository, ConcurrentDictionary<string, SemaphoreSlim>> RepositoryLocks = new();

    public async Task<ToggleFavouriteResponse> ToggleAsync(
        string userId,
        ToggleFavouriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeUserId(userId);
        if (request is null || string.IsNullOrWhiteSpace(request.ListingId))
        {
            throw new FavouriteValidationException("Mã phòng (listingId) không được để trống.");
        }

        var listingId = request.ListingId.Trim();
        var action = NormalizeAction(request.Action);

        var gate = GetUserLock(normalizedUserId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var doc = await LoadOrCreateDocumentAsync(normalizedUserId, cancellationToken);
            var existingIndex = FindIndex(doc.Items, listingId);
            bool shouldBeFavourited = action switch
            {
                "add" => true,
                "remove" => false,
                _ => existingIndex < 0
            };

            if (shouldBeFavourited)
            {
                if (existingIndex >= 0)
                {
                    var existingRecord = doc.Items[existingIndex];
                    var existingListing = await listingRepository.GetByIdAsync(existingRecord.ListingId, cancellationToken);
                    var itemDto = ToItemDto(existingRecord, existingListing);

                    return new ToggleFavouriteResponse
                    {
                        UserId = doc.UserId,
                        ListingId = existingRecord.ListingId,
                        IsFavourited = true,
                        ActionApplied = "NoChange",
                        TotalFavourites = doc.Items.Count,
                        SyncVersion = doc.SyncVersion,
                        SyncedAt = doc.UpdatedAt,
                        CurrentRent = existingListing?.Rent ?? existingRecord.SavedRent,
                        CurrentStatus = existingListing?.Status ?? "Unavailable",
                        Item = itemDto
                    };
                }

                var listing = await listingRepository.GetByIdAsync(listingId, cancellationToken)
                    ?? throw new FavouriteListingNotFoundException(listingId);

                // Tránh trùng nếu người dùng gửi alias ID khác cho cùng phòng đã lưu.
                var canonicalIndex = FindIndex(doc.Items, listing.Id);
                if (canonicalIndex >= 0)
                {
                    var existingRecord = doc.Items[canonicalIndex];
                    return new ToggleFavouriteResponse
                    {
                        UserId = doc.UserId,
                        ListingId = existingRecord.ListingId,
                        IsFavourited = true,
                        ActionApplied = "NoChange",
                        TotalFavourites = doc.Items.Count,
                        SyncVersion = doc.SyncVersion,
                        SyncedAt = doc.UpdatedAt,
                        CurrentRent = listing.Rent,
                        CurrentStatus = listing.Status,
                        Item = ToItemDto(existingRecord, listing)
                    };
                }

                var now = DateTimeOffset.UtcNow;
                var newRecord = new FavouriteRecord
                {
                    ListingId = listing.Id,
                    SavedRent = listing.Rent,
                    SavedAt = now
                };
                doc.Items.Add(newRecord);
                doc.SyncVersion++;
                doc.UpdatedAt = now;

                var saved = await favouriteRepository.SaveAsync(doc, cancellationToken);
                return new ToggleFavouriteResponse
                {
                    UserId = saved.UserId,
                    ListingId = newRecord.ListingId,
                    IsFavourited = true,
                    ActionApplied = "Added",
                    TotalFavourites = saved.Items.Count,
                    SyncVersion = saved.SyncVersion,
                    SyncedAt = saved.UpdatedAt,
                    CurrentRent = listing.Rent,
                    CurrentStatus = listing.Status,
                    Item = ToItemDto(newRecord, listing)
                };
            }
            else
            {
                var liveListing = await listingRepository.GetByIdAsync(listingId, cancellationToken);
                if (existingIndex < 0 && liveListing is not null)
                {
                    existingIndex = FindIndex(doc.Items, liveListing.Id);
                }

                if (existingIndex < 0)
                {
                    return new ToggleFavouriteResponse
                    {
                        UserId = doc.UserId,
                        ListingId = liveListing?.Id ?? listingId,
                        IsFavourited = false,
                        ActionApplied = "NoChange",
                        TotalFavourites = doc.Items.Count,
                        SyncVersion = doc.SyncVersion,
                        SyncedAt = doc.UpdatedAt,
                        CurrentRent = liveListing?.Rent,
                        CurrentStatus = liveListing?.Status,
                        Item = null
                    };
                }

                var removedId = doc.Items[existingIndex].ListingId;
                doc.Items.RemoveAt(existingIndex);
                doc.SyncVersion++;
                doc.UpdatedAt = DateTimeOffset.UtcNow;

                var saved = await favouriteRepository.SaveAsync(doc, cancellationToken);
                return new ToggleFavouriteResponse
                {
                    UserId = saved.UserId,
                    ListingId = removedId,
                    IsFavourited = false,
                    ActionApplied = "Removed",
                    TotalFavourites = saved.Items.Count,
                    SyncVersion = saved.SyncVersion,
                    SyncedAt = saved.UpdatedAt,
                    CurrentRent = liveListing?.Rent,
                    CurrentStatus = liveListing?.Status,
                    Item = null
                };
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<FavouritesListResponse> GetFavouritesAsync(
        string userId,
        bool includeUnavailable = true,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeUserId(userId);
        var doc = await LoadOrCreateDocumentAsync(normalizedUserId, cancellationToken);

        var items = await HydrateItemsAsync(doc.Items, includeUnavailable, cancellationToken);
        return new FavouritesListResponse
        {
            UserId = doc.UserId,
            TotalCount = items.Count,
            SyncVersion = doc.SyncVersion,
            SyncedAt = doc.UpdatedAt,
            Items = items
        };
    }

    public async Task<SyncFavouritesResponse> SyncAsync(
        string userId,
        SyncFavouritesRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeUserId(userId);
        if (request is null)
        {
            throw new FavouriteValidationException("Dữ liệu đồng bộ không được để trống.");
        }

        var removedSet = request.RemovedListingIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidateIds = request.ListingIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Where(id => !removedSet.Contains(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var gate = GetUserLock(normalizedUserId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var doc = await LoadOrCreateDocumentAsync(normalizedUserId, cancellationToken);
            int removedCount = 0;

            if (removedSet.Count > 0)
            {
                int before = doc.Items.Count;
                doc.Items.RemoveAll(item => removedSet.Contains(item.ListingId));
                removedCount = before - doc.Items.Count;
            }

            int addedCount = 0;
            var skippedListingIds = new List<string>();
            var now = DateTimeOffset.UtcNow;

            foreach (var candidateId in candidateIds)
            {
                if (FindIndex(doc.Items, candidateId) >= 0)
                {
                    continue;
                }

                var listing = await listingRepository.GetByIdAsync(candidateId, cancellationToken);
                if (listing is null)
                {
                    skippedListingIds.Add(candidateId);
                    continue;
                }

                if (FindIndex(doc.Items, listing.Id) >= 0)
                {
                    continue;
                }

                doc.Items.Add(new FavouriteRecord
                {
                    ListingId = listing.Id,
                    SavedRent = listing.Rent,
                    SavedAt = now
                });
                addedCount++;
            }

            if (addedCount > 0 || removedCount > 0)
            {
                doc.SyncVersion++;
                doc.UpdatedAt = now;
                doc = await favouriteRepository.SaveAsync(doc, cancellationToken);
            }

            var items = await HydrateItemsAsync(doc.Items, includeUnavailable: true, cancellationToken);
            return new SyncFavouritesResponse
            {
                UserId = doc.UserId,
                TotalCount = items.Count,
                AddedCount = addedCount,
                RemovedCount = removedCount,
                SkippedListingIds = skippedListingIds,
                SyncVersion = doc.SyncVersion,
                SyncedAt = doc.UpdatedAt,
                Items = items
            };
        }
        finally
        {
            gate.Release();
        }
    }

    private SemaphoreSlim GetUserLock(string userId)
    {
        var locks = RepositoryLocks.GetValue(
            favouriteRepository,
            static _ => new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase));
        return locks.GetOrAdd(userId, static _ => new SemaphoreSlim(1, 1));
    }

    private async Task<UserFavouritesDocument> LoadOrCreateDocumentAsync(
        string normalizedUserId,
        CancellationToken cancellationToken)
    {
        var existing = await favouriteRepository.GetByUserIdAsync(normalizedUserId, cancellationToken);
        return existing ?? new UserFavouritesDocument
        {
            Id = $"favourites-{normalizedUserId}",
            UserId = normalizedUserId,
            DocumentType = "Favourites",
            SyncVersion = 0,
            UpdatedAt = DateTimeOffset.UtcNow,
            Items = []
        };
    }

    private async Task<List<FavouriteItemDto>> HydrateItemsAsync(
        IEnumerable<FavouriteRecord> records,
        bool includeUnavailable,
        CancellationToken cancellationToken)
    {
        var ordered = records.OrderByDescending(r => r.SavedAt).ToList();
        var items = new List<FavouriteItemDto>(ordered.Count);

        foreach (var record in ordered)
        {
            var listing = await listingRepository.GetByIdAsync(record.ListingId, cancellationToken);
            var dto = ToItemDto(record, listing);
            if (includeUnavailable || dto.IsAvailable)
            {
                items.Add(dto);
            }
        }

        return items;
    }

    private static FavouriteItemDto ToItemDto(FavouriteRecord record, ListingDocument? listing)
    {
        if (listing is null)
        {
            return new FavouriteItemDto
            {
                ListingId = record.ListingId,
                Title = "Phòng trọ không còn tồn tại",
                Rent = record.SavedRent,
                SavedRent = record.SavedRent,
                Status = "Unavailable",
                IsAvailable = false,
                IsVerified = false,
                SavedAt = record.SavedAt
            };
        }

        return new FavouriteItemDto
        {
            ListingId = record.ListingId,
            CampusId = listing.CampusId,
            Title = listing.Title,
            Rent = listing.Rent,
            SavedRent = record.SavedRent,
            Area = listing.Area,
            DistanceMeters = listing.DistanceMeters,
            RoomType = listing.RoomType,
            Address = listing.Address,
            ThumbnailUrl = listing.Photos
                .Where(p => !p.Is360)
                .OrderBy(p => p.Order)
                .Select(p => p.Url)
                .FirstOrDefault() ?? string.Empty,
            Status = listing.Status,
            IsAvailable = string.Equals(listing.Status, "Available", StringComparison.OrdinalIgnoreCase),
            IsVerified = listing.IsVerified,
            SavedAt = record.SavedAt
        };
    }

    private static int FindIndex(List<FavouriteRecord> items, string listingId) =>
        items.FindIndex(item => string.Equals(item.ListingId, listingId, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new FavouriteValidationException("UserId không được để trống.");
        }

        var trimmed = userId.Trim();
        if (trimmed.Length > 100)
        {
            throw new FavouriteValidationException("UserId vượt quá độ dài cho phép (100 ký tự).");
        }

        return trimmed;
    }

    private static string NormalizeAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return "toggle";
        }

        return action.Trim().ToLowerInvariant() switch
        {
            "add" => "add",
            "remove" => "remove",
            "toggle" => "toggle",
            _ => throw new FavouriteValidationException(
                $"Hành động '{action}' không hợp lệ. Chỉ chấp nhận 'Add', 'Remove' hoặc 'Toggle'.")
        };
    }
}
