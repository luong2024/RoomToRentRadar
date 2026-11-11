using SkiaSharp;

namespace RoomToRentRadar.Api.Features.Photos;

public sealed record ProcessedPhoto(byte[] Original, byte[] Thumbnail, int Width, int Height);

public sealed class PhotoProcessor
{
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] RiffSignature = [0x52, 0x49, 0x46, 0x46];
    private static readonly byte[] WebpSignature = [0x57, 0x45, 0x42, 0x50];

    private static readonly SKSamplingOptions ImageSampling =
        new(SKFilterMode.Linear, SKMipmapMode.Linear);

    public bool TryProcess(ReadOnlySpan<byte> content, out ProcessedPhoto? photo)
    {
        photo = null;

        if (!HasSupportedSignature(content))
        {
            return false;
        }

        using var data = SKData.CreateCopy(content.ToArray());
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.FrameCount > 1)
        {
            return false;
        }

        var frameInfo = codec.Info;
        if (frameInfo.Width <= 0 || frameInfo.Height <= 0)
        {
            return false;
        }

        if ((long)frameInfo.Width * frameInfo.Height > PhotoUploadLimits.MaxPixels)
        {
            return false;
        }

        using var decoded = SKBitmap.Decode(codec);
        if (decoded is null)
        {
            return false;
        }

        var (orientation, width, height) = ResolveOrientation(codec.EncodedOrigin, decoded.Width, decoded.Height);

        using var oriented = Render(decoded, orientation, width, height);
        var original = EncodeJpeg(oriented, PhotoUploadLimits.OriginalJpegQuality);
        if (original is null)
        {
            return false;
        }

        var (thumbnailWidth, thumbnailHeight) = ResolveThumbnailSize(width, height);
        var thumbnailMatrix = SKMatrix.CreateScale(
            thumbnailWidth / (float)width,
            thumbnailHeight / (float)height);

        using var thumbnailBitmap = Render(oriented, thumbnailMatrix, thumbnailWidth, thumbnailHeight);
        var thumbnail = EncodeJpeg(thumbnailBitmap, PhotoUploadLimits.ThumbnailJpegQuality);
        if (thumbnail is null)
        {
            return false;
        }

        photo = new ProcessedPhoto(original, thumbnail, width, height);
        return true;
    }

    private static SKBitmap Render(SKBitmap source, SKMatrix matrix, int width, int height)
    {
        var target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(target);
        canvas.Clear(SKColors.White);
        canvas.Concat(in matrix);
        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(image, new SKRect(0, 0, source.Width, source.Height), ImageSampling);
        canvas.Flush();
        return target;
    }

    private static byte[]? EncodeJpeg(SKBitmap bitmap, int quality)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        return data?.ToArray();
    }

    private static (int Width, int Height) ResolveThumbnailSize(int width, int height)
    {
        var longest = Math.Max(width, height);
        if (longest <= PhotoUploadLimits.MaxThumbnailEdge)
        {
            return (width, height);
        }

        var scale = (double)PhotoUploadLimits.MaxThumbnailEdge / longest;
        return (
            Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale)));
    }

    private static (SKMatrix Matrix, int Width, int Height) ResolveOrientation(
        SKEncodedOrigin origin,
        int width,
        int height)
    {
        return origin switch
        {
            SKEncodedOrigin.TopRight => (CreateMatrix(-1, 0, width, 0, 1, 0), width, height),
            SKEncodedOrigin.BottomRight => (CreateMatrix(-1, 0, width, 0, -1, height), width, height),
            SKEncodedOrigin.BottomLeft => (CreateMatrix(1, 0, 0, 0, -1, height), width, height),
            SKEncodedOrigin.LeftTop => (CreateMatrix(0, 1, 0, 1, 0, 0), height, width),
            SKEncodedOrigin.RightTop => (CreateMatrix(0, -1, height, 1, 0, 0), height, width),
            SKEncodedOrigin.RightBottom => (CreateMatrix(0, -1, height, -1, 0, width), height, width),
            SKEncodedOrigin.LeftBottom => (CreateMatrix(0, 1, 0, -1, 0, width), height, width),
            _ => (SKMatrix.Identity, width, height)
        };
    }

    private static SKMatrix CreateMatrix(
        float scaleX,
        float skewX,
        float translateX,
        float skewY,
        float scaleY,
        float translateY) => new()
    {
        ScaleX = scaleX,
        SkewX = skewX,
        TransX = translateX,
        SkewY = skewY,
        ScaleY = scaleY,
        TransY = translateY,
        Persp0 = 0,
        Persp1 = 0,
        Persp2 = 1
    };

    private static bool HasSupportedSignature(ReadOnlySpan<byte> content)
    {
        if (content.Length >= JpegSignature.Length && content[..JpegSignature.Length].SequenceEqual(JpegSignature))
        {
            return true;
        }

        if (content.Length >= PngSignature.Length && content[..PngSignature.Length].SequenceEqual(PngSignature))
        {
            return true;
        }

        return content.Length >= 12
            && content[..RiffSignature.Length].SequenceEqual(RiffSignature)
            && content[8..12].SequenceEqual(WebpSignature);
    }
}
