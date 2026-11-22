using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Outbound;

namespace RoomToRentRadar.Adapters.Outbound.Storage;

/// <summary>
/// Uploads and deletes room gallery photos in Cloudflare R2 using S3-compatible AWS Signature V4
/// over standard HttpClient (zero external S3 SDK dependencies).
/// </summary>
public sealed class CloudflareR2PhotoStorageService : IRoomPhotoStorageService
{
    private const string Region = "auto";
    private const string Service = "s3";
    private const string ImmutableCacheControl = "public, max-age=31536000, immutable";

    private readonly HttpClient _httpClient;
    private readonly PhotoStorageOptions _options;
    private readonly TimeProvider _timeProvider;

    public CloudflareR2PhotoStorageService(
        HttpClient httpClient,
        IOptions<PhotoStorageOptions> options,
        TimeProvider? timeProvider = null)
        : this(httpClient, options?.Value ?? throw new ArgumentNullException(nameof(options)), timeProvider)
    {
    }

    public CloudflareR2PhotoStorageService(
        HttpClient httpClient,
        PhotoStorageOptions options,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? TimeProvider.System;

        if (!_options.IsCloudflareR2Configured())
        {
            throw new InvalidOperationException(
                "Cloudflare R2 storage is not fully configured. AccountId, BucketName, AccessKeyId, SecretAccessKey, and PublicBaseUrl are required.");
        }
    }

    public async Task<StoredPhotoResult> UploadAsync(
        string roomId,
        string photoId,
        ProcessedImageResult processedImage,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentException.ThrowIfNullOrWhiteSpace(photoId);
        ArgumentNullException.ThrowIfNull(processedImage);

        var safeRoomSegment = SanitizePathSegment(roomId);
        var safePhotoSegment = SanitizePathSegment(photoId);
        var extension = processedImage.FileExtension.StartsWith('.')
            ? processedImage.FileExtension
            : "." + processedImage.FileExtension;

        var objectKey = $"rooms/{safeRoomSegment}/{safePhotoSegment}{extension}";
        var thumbnailKey = $"rooms/{safeRoomSegment}/{safePhotoSegment}_thumb{extension}";

        await PutObjectAsync(objectKey, processedImage.CompressedBytes, processedImage.ContentType, cancellationToken);

        try
        {
            await PutObjectAsync(thumbnailKey, processedImage.ThumbnailBytes, processedImage.ContentType, cancellationToken);
        }
        catch
        {
            try
            {
                await DeleteObjectAsync(objectKey, CancellationToken.None);
            }
            catch
            {
                // Preserve original upload failure.
            }

            throw;
        }

        var publicBase = _options.PublicBaseUrl.TrimEnd('/');
        return new StoredPhotoResult(
            Url: $"{publicBase}/{objectKey}",
            ThumbnailUrl: $"{publicBase}/{thumbnailKey}",
            ObjectKey: objectKey,
            ThumbnailObjectKey: thumbnailKey);
    }

    public async Task DeleteAsync(
        string objectKey,
        string thumbnailObjectKey,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(objectKey))
        {
            await DeleteObjectAsync(objectKey, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(thumbnailObjectKey))
        {
            await DeleteObjectAsync(thumbnailObjectKey, cancellationToken);
        }
    }

    private async Task PutObjectAsync(
        string objectKey,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken)
    {
        var requestUri = BuildObjectUri(objectKey);
        using var request = new HttpRequestMessage(HttpMethod.Put, requestUri)
        {
            Content = new ByteArrayContent(content)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Headers.TryAddWithoutValidation("Cache-Control", ImmutableCacheControl);

        SignRequest(request, content, _timeProvider.GetUtcNow());

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken)
    {
        var requestUri = BuildObjectUri(objectKey);
        using var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);

        SignRequest(request, [], _timeProvider.GetUtcNow());

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }
    }

    private Uri BuildObjectUri(string objectKey)
    {
        var encodedKey = string.Join(
            "/",
            objectKey.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));

        return new Uri($"https://{_options.AccountId}.r2.cloudflarestorage.com/{Uri.EscapeDataString(_options.BucketName)}/{encodedKey}");
    }

    internal void SignRequest(HttpRequestMessage request, byte[] payload, DateTimeOffset timestampUtc)
    {
        var uri = request.RequestUri!;
        var amzDate = timestampUtc.UtcDateTime.ToString("yyyyMMddTHHmmssZ");
        var dateStamp = timestampUtc.UtcDateTime.ToString("yyyyMMdd");
        var payloadHash = ToLowerHex(SHA256.HashData(payload));

        request.Headers.Host = uri.Host;
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);

        var canonicalUri = uri.AbsolutePath;
        var canonicalQueryString = string.Empty;
        var canonicalHeaders =
            $"host:{uri.Host}\n" +
            $"x-amz-content-sha256:{payloadHash}\n" +
            $"x-amz-date:{amzDate}\n";
        const string signedHeaders = "host;x-amz-content-sha256;x-amz-date";

        var canonicalRequest =
            $"{request.Method.Method}\n" +
            $"{canonicalUri}\n" +
            $"{canonicalQueryString}\n" +
            $"{canonicalHeaders}\n" +
            $"{signedHeaders}\n" +
            $"{payloadHash}";

        var credentialScope = $"{dateStamp}/{Region}/{Service}/aws4_request";
        var stringToSign =
            $"AWS4-HMAC-SHA256\n" +
            $"{amzDate}\n" +
            $"{credentialScope}\n" +
            $"{ToLowerHex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";

        var signingKey = DeriveSigningKey(_options.SecretAccessKey, dateStamp, Region, Service);
        var signature = ToLowerHex(HmacSha256(signingKey, stringToSign));

        var authorizationHeader =
            $"AWS4-HMAC-SHA256 Credential={_options.AccessKeyId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
        request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);
    }

    private static byte[] DeriveSigningKey(string secretKey, string dateStamp, string region, string service)
    {
        var kSecret = Encoding.UTF8.GetBytes("AWS4" + secretKey);
        var kDate = HmacSha256(kSecret, dateStamp);
        var kRegion = HmacSha256(kDate, region);
        var kService = HmacSha256(kRegion, service);
        return HmacSha256(kService, "aws4_request");
    }

    private static byte[] HmacSha256(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string ToLowerHex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    private static string SanitizePathSegment(string segment)
    {
        var trimmed = segment.Trim();
        var sb = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (char.IsLetterOrDigit(ch) || ch is '-' or '_')
            {
                sb.Append(ch);
            }
        }

        return sb.Length > 0 ? sb.ToString() : "item";
    }
}
