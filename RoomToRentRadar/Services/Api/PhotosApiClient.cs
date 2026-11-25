using System.Net;
using System.Net.Http.Json;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Services.Api;

public sealed class PhotosApiClient(HttpClient httpClient) : IPhotosApiClient
{
    public async Task<ListingPhotosFetchResult> GetPhotosAsync(
        Guid listingId,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"api/v1/listings/{listingId}/photos",
            cancellationToken);

        // 404 and 410 are expected UI states for a gallery, not failures.
        if (response.StatusCode == HttpStatusCode.NotFound)
            return new ListingPhotosFetchResult(ListingPhotosFetchOutcome.NotFound, []);

        if (response.StatusCode == HttpStatusCode.Gone)
            return new ListingPhotosFetchResult(ListingPhotosFetchOutcome.Unavailable, []);

        response.EnsureSuccessStatusCode();

        var photos = await response.Content.ReadFromJsonAsync<List<ListingPhotoDto>>(
            ApiJson.Options,
            cancellationToken);

        return photos is null
            ? throw new InvalidOperationException("The listing photos endpoint returned an empty response.")
            : new ListingPhotosFetchResult(ListingPhotosFetchOutcome.Found, photos);
    }

    public async Task<byte[]?> GetMediaBytesAsync(
        string absoluteUrl,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(absoluteUrl, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}
