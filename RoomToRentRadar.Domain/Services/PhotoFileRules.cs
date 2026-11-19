namespace RoomToRentRadar.Domain.Services;

/// <summary>
/// Media rules of BR-MED-01..04: accepted formats and size (magic bytes, never file extension),
/// per-listing photo counts and the 24 h lifetime of unattached uploads.
/// </summary>
public static class PhotoFileRules
{
    /// <summary>BR-MED-02: ≤ 10 MB per file.</summary>
    public const long MaxFileBytes = 10L * 1024 * 1024;

    /// <summary>BR-MED-03: unattached uploads expire after 24 h.</summary>
    public static readonly TimeSpan UnattachedPhotoLifetime = TimeSpan.FromHours(24);

    /// <summary>BR-MED-01: 3–20 standard photos per listing.</summary>
    public const int MinStandardPhotos = 3;
    public const int MaxStandardPhotos = 20;

    /// <summary>BR-MED-01: 0–3 panoramas per listing.</summary>
    public const int MaxPanoramaPhotos = 3;

    private static ReadOnlySpan<byte> JpegMagic => [0xFF, 0xD8, 0xFF];

    private static ReadOnlySpan<byte> PngMagic => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Detects the real image format from magic bytes (BR-MED-02): JPEG <c>FF D8 FF</c> → ".jpg",
    /// PNG <c>89 50 4E 47 0D 0A 1A 0A</c> → ".png"; anything else → null.
    /// </summary>
    public static string? DetectExtension(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(JpegMagic))
        {
            return ".jpg";
        }

        if (header.StartsWith(PngMagic))
        {
            return ".png";
        }

        return null;
    }

    /// <summary>BR-MED-02: a panorama must have a 2:1 aspect ratio with ±2% tolerance.</summary>
    public static bool IsPanoramaRatio(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        return Math.Abs(width - 2.0 * height) <= 0.04 * height;
    }
}
