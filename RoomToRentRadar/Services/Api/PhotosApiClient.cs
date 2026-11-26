using System.Net;
using System.Net.Http.Headers;
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

    public async Task<ListingPhotosUploadResult> UploadPhotosAsync(
        Guid listingId,
        IReadOnlyList<ListingPhotoUpload> files,
        ListingPhotoKind kind,
        CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(
            new StringContent(kind == ListingPhotoKind.Panorama ? "panorama" : "photo"),
            "kind");

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stream = await file.OpenReadAsync();
            var fileContent = new StreamContent(stream);
            // Attach before sending so the multipart content disposes the stream too.
            content.Add(fileContent, "files", Path.GetFileName(file.FileName));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        }

        using var response = await httpClient.PostAsync(
            $"api/v1/listings/{listingId}/photos",
            content,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Created)
        {
            var photos = await response.Content.ReadFromJsonAsync<List<ListingPhotoDto>>(
                ApiJson.Options,
                cancellationToken);

            return photos is null
                ? throw new InvalidOperationException("The photo upload endpoint returned an empty response.")
                : new ListingPhotosUploadResult(ListingPhotosUploadOutcome.Uploaded, photos);
        }

        return new ListingPhotosUploadResult(MapFailure(response.StatusCode), []);
    }

    private static ListingPhotosUploadOutcome MapFailure(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => ListingPhotosUploadOutcome.Unauthorized,
        HttpStatusCode.Forbidden => ListingPhotosUploadOutcome.Forbidden,
        HttpStatusCode.NotFound => ListingPhotosUploadOutcome.ListingNotFound,
        HttpStatusCode.Gone => ListingPhotosUploadOutcome.ListingUnavailable,
        HttpStatusCode.BadRequest => ListingPhotosUploadOutcome.InvalidPhoto,
        HttpStatusCode.Conflict => ListingPhotosUploadOutcome.PhotoLimitExceeded,
        HttpStatusCode.RequestEntityTooLarge => ListingPhotosUploadOutcome.PhotoTooLarge,
        HttpStatusCode.UnsupportedMediaType => ListingPhotosUploadOutcome.UnsupportedType,
        _ => ListingPhotosUploadOutcome.Failed
    };

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
