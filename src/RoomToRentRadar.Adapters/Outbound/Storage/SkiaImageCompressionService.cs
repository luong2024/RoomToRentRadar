using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Exceptions;
using SkiaSharp;

namespace RoomToRentRadar.Adapters.Outbound.Storage;

/// <summary>
/// Validates image magic bytes, strips EXIF metadata, compresses the primary image
/// (standard or 360° panorama), and generates a lightweight progressive thumbnail via SkiaSharp.
/// </summary>
public sealed class SkiaImageCompressionService : IImageCompressionService
{
    public const int StandardMaxDimensionPx = 1920;
    public const int PanoramaMaxWidthPx = 4096;
    public const int ThumbnailMaxWidthPx = 480;
    public const int MaxDecodedWidthPx = 12000;
    public const int MaxDecodedHeightPx = 12000;
    public const long MaxDecodedPixelCount = 50_000_000L; // 50 MP max decoded budget (~200 MB RGBA)
    public const int StandardQuality = 82;
    public const int PanoramaQuality = 85;
    public const int ThumbnailQuality = 75;

    private static readonly HashSet<string> AllowedDeclaredContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png"
    };

    public Task<ProcessedImageResult> ProcessAsync(
        byte[] rawBytes,
        string declaredContentType,
        bool is360,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (rawBytes is null || rawBytes.Length == 0)
        {
            throw new InvalidRoomPhotoException("Photo file cannot be empty.");
        }

        if (rawBytes.Length > RoomPhoto.MaxFileSizeBytes)
        {
            throw new InvalidRoomPhotoException(
                $"Photo file exceeds the maximum allowed size of {RoomPhoto.MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        if (!string.IsNullOrWhiteSpace(declaredContentType) &&
            !AllowedDeclaredContentTypes.Contains(declaredContentType.Trim()))
        {
            throw new InvalidRoomPhotoException("Only JPEG and PNG images are supported.");
        }

        if (!IsSupportedImageMagicBytes(rawBytes))
        {
            throw new InvalidRoomPhotoException("Invalid image file format. Only valid JPEG and PNG files are allowed.");
        }

        using var stream = new SKMemoryStream(rawBytes);
        using var codec = SKCodec.Create(stream)
            ?? throw new InvalidRoomPhotoException("Unable to decode image content. The file may be corrupted.");

        var origin = codec.EncodedOrigin;
        var decodedWidth = codec.Info.Width;
        var decodedHeight = codec.Info.Height;
        if (decodedWidth <= 0 || decodedHeight <= 0)
        {
            throw new InvalidRoomPhotoException("Image dimensions must be positive.");
        }

        if (decodedWidth > MaxDecodedWidthPx || decodedHeight > MaxDecodedHeightPx)
        {
            throw new InvalidRoomPhotoException(
                $"Image dimensions ({decodedWidth}x{decodedHeight}) exceed the maximum allowed size of {MaxDecodedWidthPx}x{MaxDecodedHeightPx} pixels.");
        }

        var totalPixels = (long)decodedWidth * decodedHeight;
        if (totalPixels > MaxDecodedPixelCount)
        {
            throw new InvalidRoomPhotoException(
                $"Image pixel count ({totalPixels:N0}) exceeds the maximum allowed limit of {MaxDecodedPixelCount:N0} pixels.");
        }

        var info = new SKImageInfo(decodedWidth, decodedHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var decodedBitmap = new SKBitmap(info);
        var decodeResult = codec.GetPixels(decodedBitmap.Info, decodedBitmap.GetPixels());
        if (decodeResult != SKCodecResult.Success && decodeResult != SKCodecResult.IncompleteInput)
        {
            throw new InvalidRoomPhotoException("Unable to decode image pixels.");
        }

        using var orientedBitmap = ApplyExifOrientation(decodedBitmap, origin);

        var maxTargetWidth = is360 ? PanoramaMaxWidthPx : StandardMaxDimensionPx;
        var maxTargetHeight = is360 ? PanoramaMaxWidthPx : StandardMaxDimensionPx;
        var quality = is360 ? PanoramaQuality : StandardQuality;

        using var mainBitmap = ResizeIfNeeded(orientedBitmap, maxTargetWidth, maxTargetHeight);
        var compressedBytes = EncodeJpeg(mainBitmap, quality);

        using var thumbnailBitmap = ResizeIfNeeded(mainBitmap, ThumbnailMaxWidthPx, ThumbnailMaxWidthPx);
        var thumbnailBytes = EncodeJpeg(thumbnailBitmap, ThumbnailQuality);

        var result = new ProcessedImageResult(
            CompressedBytes: compressedBytes,
            ThumbnailBytes: thumbnailBytes,
            ContentType: "image/jpeg",
            FileExtension: ".jpg",
            Width: mainBitmap.Width,
            Height: mainBitmap.Height);

        return Task.FromResult(result);
    }

    public static bool IsSupportedImageMagicBytes(ReadOnlySpan<byte> bytes)
    {
        // JPEG: FF D8 FF
        var isJpeg = bytes.Length >= 3 &&
                     bytes[0] == 0xFF &&
                     bytes[1] == 0xD8 &&
                     bytes[2] == 0xFF;

        if (isJpeg)
        {
            return true;
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        var isPng = bytes.Length >= 8 &&
                    bytes[0] == 0x89 &&
                    bytes[1] == 0x50 &&
                    bytes[2] == 0x4E &&
                    bytes[3] == 0x47 &&
                    bytes[4] == 0x0D &&
                    bytes[5] == 0x0A &&
                    bytes[6] == 0x1A &&
                    bytes[7] == 0x0A;

        return isPng;
    }

    private static SKBitmap ApplyExifOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft or SKEncodedOrigin.Default)
        {
            return source.Copy();
        }

        var swapDimensions = origin is SKEncodedOrigin.RightTop or SKEncodedOrigin.LeftBottom
            or SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightBottom;

        var targetWidth = swapDimensions ? source.Height : source.Width;
        var targetHeight = swapDimensions ? source.Width : source.Height;

        var rotated = new SKBitmap(targetWidth, targetHeight, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(rotated);
        canvas.Clear(SKColors.White);

        switch (origin)
        {
            case SKEncodedOrigin.BottomRight:
                canvas.RotateDegrees(180, targetWidth / 2f, targetHeight / 2f);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(targetWidth, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, targetHeight);
                canvas.RotateDegrees(270);
                break;
            case SKEncodedOrigin.TopRight:
                canvas.Scale(-1, 1, targetWidth / 2f, targetHeight / 2f);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Scale(1, -1, targetWidth / 2f, targetHeight / 2f);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.Translate(targetWidth, 0);
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1, source.Width / 2f, source.Height / 2f);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Translate(0, targetHeight);
                canvas.RotateDegrees(270);
                canvas.Scale(1, -1, source.Width / 2f, source.Height / 2f);
                break;
        }

        canvas.DrawBitmap(source, 0, 0);
        canvas.Flush();
        return rotated;
    }

    private static SKBitmap ResizeIfNeeded(SKBitmap source, int maxWidth, int maxHeight)
    {
        if (source.Width <= maxWidth && source.Height <= maxHeight)
        {
            return source.Copy();
        }

        var ratioX = (double)maxWidth / source.Width;
        var ratioY = (double)maxHeight / source.Height;
        var ratio = Math.Min(ratioX, ratioY);

        var newWidth = Math.Max(1, (int)Math.Round(source.Width * ratio));
        var newHeight = Math.Max(1, (int)Math.Round(source.Height * ratio));

        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        return source.Resize(new SKImageInfo(newWidth, newHeight, source.ColorType, source.AlphaType), sampling)
            ?? throw new InvalidRoomPhotoException("Failed to resize image.");
    }

    private static byte[] EncodeJpeg(SKBitmap bitmap, int quality)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, quality)
            ?? throw new InvalidRoomPhotoException("Failed to encode compressed JPEG image.");
        return encoded.ToArray();
    }
}
