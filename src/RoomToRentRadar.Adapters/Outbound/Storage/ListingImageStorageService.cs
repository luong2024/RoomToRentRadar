namespace RoomToRentRadar.Adapters.Outbound.Storage;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using SkiaSharp;

public sealed class ListingImageStorageService : IListingImageStorageService
{
    public const long MaxImageSizeBytes = 10L * 1024 * 1024;

    private readonly BlobContainerClient _container;

    public ListingImageStorageService(
        IConfiguration configuration,
        IHostEnvironment environment,
        BlobContainerClient? containerClient = null)
    {
        var connectionString = configuration["ListingImageStorage:ConnectionString"]
            ?? configuration["AzureStorage:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString) && (environment.IsDevelopment() || environment.IsEnvironment("Test") || environment.IsEnvironment("Testing")))
        {
            connectionString = "UseDevelopmentStorage=true";
        }
        if (containerClient is null && string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ListingImageStorage:ConnectionString or AzureStorage:ConnectionString must be configured.");
        }

        var containerName = configuration["ListingImageStorage:ContainerName"] ?? "listing-images";
        _container = containerClient ?? new BlobContainerClient(connectionString!, containerName);
    }

    public async Task<string> StoreAsync(
        Guid ownerId,
        string listingId,
        Stream content,
        string fileName,
        string contentType,
        long length,
        CancellationToken cancellationToken = default)
    {
        if (ownerId == Guid.Empty || string.IsNullOrWhiteSpace(listingId))
        {
            throw new RequestValidationException("image", "A valid owner and listing are required for image storage.");
        }

        var validated = await ListingImageValidator.ReadAndValidateAsync(content, fileName, contentType, length, cancellationToken);
        var blobName = BuildBlobName(ownerId, listingId, validated.Extension);
        var blob = _container.GetBlobClient(blobName);
        await _container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
        await using var stream = new MemoryStream(validated.Bytes, writable: false);
        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = validated.ContentType,
                CacheControl = "public, max-age=31536000, immutable"
            },
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
        }, cancellationToken);
        return blob.Uri.AbsoluteUri;
    }

    public async Task DeleteAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Host, _container.Uri.Host, StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.StartsWith(_container.Uri.AbsolutePath.TrimEnd('/') + "/", StringComparison.Ordinal))
        {
            throw new RequestValidationException("imageUrl", "The image URL does not belong to listing image storage.");
        }

        var blobName = Uri.UnescapeDataString(uri.AbsolutePath[(_container.Uri.AbsolutePath.TrimEnd('/').Length + 1)..]);
        await _container.GetBlobClient(blobName).DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
    }

    internal static string BuildBlobName(Guid ownerId, string listingId, string validatedExtension) =>
        $"{ownerId:D}/{listingId}/{Guid.NewGuid():N}{validatedExtension}";
}

internal sealed record ValidatedListingImage(byte[] Bytes, string Extension, string ContentType);

internal static class ListingImageValidator
{
    public static async Task<ValidatedListingImage> ReadAndValidateAsync(
        Stream content,
        string fileName,
        string contentType,
        long declaredLength,
        CancellationToken cancellationToken)
    {
        if (content is null || !content.CanRead || declaredLength <= 0)
            throw new RequestValidationException("photos", "Image files must not be empty.");
        if (declaredLength > ListingImageStorageService.MaxImageSizeBytes)
            throw new RequestValidationException("photos", "Each image must be at most 10 MiB.");

        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        var normalizedType = (contentType ?? string.Empty).Split(';', 2)[0].Trim().ToLowerInvariant();
        var isJpeg = (extension is ".jpg" or ".jpeg") && (normalizedType is "image/jpeg" or "image/jpg");
        var isPng = extension == ".png" && normalizedType == "image/png";
        if (!isJpeg && !isPng)
            throw new RequestValidationException("photos", "Only matching JPG, JPEG, and PNG file extensions and MIME types are allowed.");

        await using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await content.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (output.Length + read > ListingImageStorageService.MaxImageSizeBytes)
                throw new RequestValidationException("photos", "Each image must be at most 10 MiB.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        var bytes = output.ToArray();
        if (bytes.LongLength == 0 || bytes.LongLength != declaredLength)
            throw new RequestValidationException("photos", "Image content length is invalid.");

        var hasJpegSignature = bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        var hasPngSignature = bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        if ((isJpeg && !hasJpegSignature) || (isPng && !hasPngSignature))
            throw new RequestValidationException("photos", "The image content does not match its declared format.");

        SKBitmap? bitmap;
        try
        {
            bitmap = SKBitmap.Decode(bytes);
        }
        catch (ArgumentNullException)
        {
            throw new RequestValidationException("photos", "The image content is corrupted or unsupported.");
        }

        using (bitmap)
        {
            if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
            {
                throw new RequestValidationException("photos", "The image content is corrupted or unsupported.");
            }
        }

        var canonicalExtension = isPng ? ".png" : extension == ".jpeg" ? ".jpeg" : ".jpg";
        return new ValidatedListingImage(bytes, canonicalExtension, isPng ? "image/png" : "image/jpeg");
    }
}
