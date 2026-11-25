using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Infrastructure.Photos;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.SqliteDataAccess;
using RoomToRentRadar.SqliteDataAccess.Seed;
using SkiaSharp;

namespace RoomToRentRadar.Api.Seed;

/// <summary>
/// Writes the placeholder JPEGs referenced by the seeded photo rows. Every image is drawn
/// procedurally with SkiaSharp, so the repository ships no third-party image assets and
/// carries no extra licence obligation.
/// </summary>
public sealed class SeedMediaWriter(PhotoStorageOptions options, ILogger<SeedMediaWriter> logger)
{
    private const string MediaUrlPrefix = PhotoStorage.RequestPath + "/";
    private const int PhotoWidth = 1600;
    private const int PhotoHeight = 1067;
    private const int ThumbnailWidth = 480;
    private const int PanoramaWidth = 2048;
    private const int PanoramaHeight = 1024;
    private const int JpegQuality = 80;
    private const string PanoramaLabel = "360°";

    public int EnsureFiles(IReadOnlyList<ListingPhoto> photos)
    {
        var written = 0;

        foreach (var photo in photos)
        {
            written += WriteIfMissing(photo.Url, photo.Kind, photo.SortOrder, thumbnail: false);
            written += WriteIfMissing(photo.ThumbUrl, photo.Kind, photo.SortOrder, thumbnail: true);
        }

        return written;
    }

    public async Task<int> EnsureSampleMediaAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var listingIds = DatabaseSeeder.GetSampleListingIds();
        var photos = await dbContext.ListingPhotos
            .AsNoTracking()
            .Where(photo => listingIds.Contains(photo.ListingId))
            .OrderBy(photo => photo.ListingId)
            .ThenBy(photo => photo.SortOrder)
            .ToListAsync(cancellationToken);

        var written = EnsureFiles(photos);
        logger.LogInformation(
            "Seed media generation wrote {FileCount} placeholder files under {MediaRootPath}.",
            written,
            options.RootPath);

        return written;
    }

    private int WriteIfMissing(string? url, ListingPhotoKind kind, int sortOrder, bool thumbnail)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith(MediaUrlPrefix, StringComparison.Ordinal))
        {
            return 0;
        }

        var path = Path.Combine(
            options.RootPath,
            url[MediaUrlPrefix.Length..].Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path))
        {
            return 0;
        }

        var (width, height) = ResolveSize(kind, thumbnail);
        var label = kind == ListingPhotoKind.Panorama ? PanoramaLabel : $"Ảnh {sortOrder + 1}";
        var content = CreateImage(url, kind, width, height, label);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        return 1;
    }

    private static (int Width, int Height) ResolveSize(ListingPhotoKind kind, bool thumbnail)
    {
        var (width, height) = kind == ListingPhotoKind.Panorama
            ? (PanoramaWidth, PanoramaHeight)
            : (PhotoWidth, PhotoHeight);

        if (!thumbnail)
        {
            return (width, height);
        }

        return (ThumbnailWidth, Math.Max(1, (int)Math.Round(ThumbnailWidth * (double)height / width)));
    }

    private static byte[] CreateImage(
        string url,
        ListingPhotoKind kind,
        int width,
        int height,
        string label)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        var hue = hash[0] * 360f / 255f;

        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);

        if (kind == ListingPhotoKind.Panorama)
        {
            DrawPanorama(canvas, width, height, hue);
        }
        else
        {
            DrawPhoto(canvas, width, height, hue);
        }

        DrawLabel(canvas, width, height, label);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return data!.ToArray();
    }

    private static void DrawPhoto(SKCanvas canvas, int width, int height, float hue)
    {
        var top = SKColor.FromHsl(hue, 42f, 56f);
        var bottom = SKColor.FromHsl((hue + 45f) % 360f, 38f, 38f);

        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(0, height),
            [top, bottom],
            SKShaderTileMode.Clamp);
        using var paint = new SKPaint { Shader = shader };
        canvas.DrawRect(new SKRect(0, 0, width, height), paint);

        paint.Shader = null;
        paint.Color = new SKColor(0, 0, 0, 96);
        canvas.DrawRect(new SKRect(0, height * 0.78f, width, height), paint);
    }

    private static void DrawPanorama(SKCanvas canvas, int width, int height, float hue)
    {
        var horizon = height / 2f;
        var skyTop = SKColor.FromHsl(hue, 58f, 62f);
        var skyHorizon = SKColor.FromHsl(hue, 32f, 88f);
        var groundHorizon = SKColor.FromHsl((hue + 150f) % 360f, 30f, 42f);
        var groundBottom = SKColor.FromHsl((hue + 150f) % 360f, 34f, 24f);

        using var skyShader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(0, horizon),
            [skyTop, skyHorizon],
            SKShaderTileMode.Clamp);
        using var paint = new SKPaint { Shader = skyShader };
        canvas.DrawRect(new SKRect(0, 0, width, horizon), paint);

        using var groundShader = SKShader.CreateLinearGradient(
            new SKPoint(0, horizon),
            new SKPoint(0, height),
            [groundHorizon, groundBottom],
            SKShaderTileMode.Clamp);
        paint.Shader = groundShader;
        canvas.DrawRect(new SKRect(0, horizon, width, height), paint);

        paint.Shader = null;
        paint.Color = new SKColor(255, 255, 255, 160);
        paint.StrokeWidth = Math.Max(2f, height / 256f);
        canvas.DrawLine(0, horizon, width, horizon, paint);
    }

    private static void DrawLabel(SKCanvas canvas, int width, int height, string label)
    {
        using var typeface = SKTypeface.Default;
        if (typeface is null)
        {
            return;
        }

        using var font = new SKFont(typeface, Math.Max(12f, Math.Min(width, height) / 9f));
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        var metrics = font.Metrics;
        var baseline = height / 2f - (metrics.Ascent + metrics.Descent) / 2f;
        canvas.DrawText(label, width / 2f, baseline, SKTextAlign.Center, font, paint);
    }
}
