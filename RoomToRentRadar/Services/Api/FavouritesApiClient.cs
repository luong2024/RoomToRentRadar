using System.Net;
using System.Net.Http.Json;
using RoomToRentRadar.Shared.Favourites;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Services.Api;

public sealed class FavouritesApiClient(HttpClient httpClient) : IFavouritesApiClient
{
    public async Task<FavouritesFetchResult> GetFavouritesAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/v1/favourites", cancellationToken);

        // Signed out is a normal UI state: the heart starts empty and the
        // favourites screen shows its sign-in prompt.
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new FavouritesFetchResult(FavouritesFetchOutcome.Unauthorized, []);
        }

        response.EnsureSuccessStatusCode();

        var favourites = await response.Content.ReadFromJsonAsync<List<FavouriteRoomDto>>(
            ApiJson.Options,
            cancellationToken);

        return favourites is null
            ? throw new InvalidOperationException("The favourites endpoint returned an empty response.")
            : new FavouritesFetchResult(FavouritesFetchOutcome.Found, favourites);
    }

    public async Task AddFavouriteAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsync(
            $"api/v1/favourites/{listingId}",
            content: null,
            cancellationToken);

        // The listing vanished between browsing and tapping: there is nothing to save.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveFavouriteAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync(
            $"api/v1/favourites/{listingId}",
            cancellationToken);

        // Deleting twice ends in the same state, so a missing row is fine.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }
}
