using System.Text.RegularExpressions;

namespace RoomToRentRadar.Domain.Services;

public static class PhotoFileRules
{
    public const long MaxFileBytes = 10 * 1024 * 1024;
    public const int MaxPhotosPerRoom = 10;
    public const int MinPhotosToPublish = 3;

    private static readonly byte[] JpgMagicBytes = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] PngMagicBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static readonly Regex RoomIdPattern = new(
        @"\A[A-Za-z0-9_-]{1,64}\z",
        RegexOptions.CultureInvariant);

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

    public static bool IsValidRoomId(string? roomId)
        => !string.IsNullOrWhiteSpace(roomId) && RoomIdPattern.IsMatch(roomId);
}
