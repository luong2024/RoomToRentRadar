using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Models.RoomListings;

namespace RoomToRentRadar.Services.RoomListings;

public sealed class FavouritesService(IApiClient apiClient) : IFavouritesService
{
    private readonly IApiClient _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));

    public async Task<IReadOnlyList<RoomListItemModel>> GetMyFavouritesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetAsync<FavouriteListResponse>("api/v1/favourites", cancellationToken);
        return (result?.Items ?? [])
            .Where(item => !item.IsDeleted && !string.IsNullOrWhiteSpace(item.RoomId))
            .Select(item => new RoomListItemModel
            {
                Id = item.RoomId,
                Title = item.Title,
                Price = item.Price,
                AreaSqm = item.AreaSqm > 0 ? item.AreaSqm : item.Area,
                Address = item.Address,
                RoomType = string.IsNullOrWhiteSpace(item.RoomType) ? "private" : item.RoomType,
                ThumbnailUrl = item.ThumbnailUrl ?? (item.ImageUrls ?? []).FirstOrDefault(),
                IsVerified = item.IsVerified,
                IsAvailable = item.IsAvailable,
                Amenities = item.Amenities ?? [],
                IsFavorite = true
            })
            .ToArray();
    }

    public async Task<IReadOnlySet<string>> GetMyFavouriteIdsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetAsync<FavouriteIdsResponse>("api/v1/favourites/ids", cancellationToken);
        return new HashSet<string>(result?.RoomIds ?? [], StringComparer.Ordinal);
    }

    public async Task AddFavouriteAsync(string roomId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        await _apiClient.PostAsync<AddFavouriteRequest, FavouriteResponse>(
            "api/v1/favourites",
            new AddFavouriteRequest(roomId),
            cancellationToken);
    }

    public Task RemoveFavouriteAsync(string roomId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        return _apiClient.DeleteAsync($"api/v1/favourites/{Uri.EscapeDataString(roomId)}", cancellationToken);
    }

    public sealed record AddFavouriteRequest(string RoomId);
    public sealed class FavouriteListResponse { public IReadOnlyList<FavouriteResponse> Items { get; init; } = []; }
    public sealed class FavouriteIdsResponse { public IReadOnlyList<string> RoomIds { get; init; } = []; }
    public sealed class FavouriteResponse
    {
        public string RoomId { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public decimal Price { get; init; }
        public double Area { get; init; }
        public double AreaSqm { get; init; }
        public string Address { get; init; } = string.Empty;
        public string RoomType { get; init; } = string.Empty;
        public IReadOnlyList<string> Amenities { get; init; } = [];
        public IReadOnlyList<string> ImageUrls { get; init; } = [];
        public string? ThumbnailUrl { get; init; }
        public bool IsVerified { get; init; }
        public bool IsAvailable { get; init; }
        public bool IsDeleted { get; init; }
    }
}
