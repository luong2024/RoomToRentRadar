using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Services;

/// <summary>
/// Đồng bộ yêu thích với Backend T06.1 qua Refit; tự động fallback sang MockFavouritesService khi API chưa bật.
/// </summary>
public sealed class FavouritesService : IFavouritesService
{
    private readonly IFavouritesApiService _apiService;
    private readonly MockFavouritesService _mockService;
    private readonly ILogger<FavouritesService> _logger;

    public FavouritesService(
        IFavouritesApiService apiService,
        MockFavouritesService mockService,
        ILogger<FavouritesService> logger)
    {
        _apiService = apiService;
        _mockService = mockService;
        _logger = logger;
        _mockService.FavouritesChanged += (_, e) => FavouritesChanged?.Invoke(this, e);
    }

    public event EventHandler? FavouritesChanged;

    public async Task<IReadOnlyList<RoomListingItemModel>> GetFavouritesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiService.GetFavouritesAsync(cancellationToken);
            return response.Items
                .Select(dto =>
                {
                    var item = RoomListingItemModel.FromDto(dto);
                    item.IsFavourite = true;
                    return item;
                })
                .ToList();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Backend T06.1 chưa khả dụng, dùng MockFavouritesService.");
            return await _mockService.GetFavouritesAsync(cancellationToken);
        }
    }

    public async Task<bool> IsFavouriteAsync(string listingId, CancellationToken cancellationToken = default)
    {
        var items = await GetFavouritesAsync(cancellationToken);
        return items.Any(r => string.Equals(r.Id, listingId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> ToggleFavouriteAsync(RoomListingItemModel room, CancellationToken cancellationToken = default)
    {
        bool currentlyFav = await _mockService.IsFavouriteAsync(room.Id, cancellationToken);
        if (currentlyFav)
        {
            await RemoveFavouriteAsync(room.Id, cancellationToken);
            room.IsFavourite = false;
            return false;
        }

        await AddFavouriteAsync(room, cancellationToken);
        room.IsFavourite = true;
        return true;
    }

    public async Task AddFavouriteAsync(RoomListingItemModel room, CancellationToken cancellationToken = default)
    {
        await _mockService.AddFavouriteAsync(room, cancellationToken);
        try
        {
            await _apiService.AddFavouriteAsync(room.Id, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Chưa đồng bộ được AddFavourite {ListingId} lên Backend T06.1.", room.Id);
        }
    }

    public async Task RemoveFavouriteAsync(string listingId, CancellationToken cancellationToken = default)
    {
        await _mockService.RemoveFavouriteAsync(listingId, cancellationToken);
        try
        {
            await _apiService.RemoveFavouriteAsync(listingId, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Chưa đồng bộ được RemoveFavourite {ListingId} lên Backend T06.1.", listingId);
        }
    }
}