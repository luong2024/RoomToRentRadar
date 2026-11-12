namespace RoomToRentRadar.Domain.Services;

public static class PhotoFileRules
{
    public const long MaxFileBytes = 10 * 1024 * 1024;

    // BR-MED-03: an uploaded photo that is never attached expires after 24 h.
    public static readonly TimeSpan UnattachedPhotoLifetime = TimeSpan.FromHours(24);

    private static readonly byte[] JpgMagicBytes = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] PngMagicBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static string? DetectExtension(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(JpgMagicBytes))
        {
            return "jpg";
        }

        if (header.StartsWith(PngMagicBytes))
        {
            return "png";
        }

        return null;
    }

    // 2:1 plus/minus 2%, compared without floating-point division at the boundary
    // (3920/2000.0 - 2.0 evaluates to 0.040000000000000036).
    public static bool IsPanoramaRatio(int width, int height)
        => height > 0 && Math.Abs(width - 2.0 * height) <= 0.04 * height;

    public static string? ContentTypeOf(string? extension) => extension switch
    {
        "jpg" => "image/jpeg",
        "png" => "image/png",
        _ => null
    };
}
