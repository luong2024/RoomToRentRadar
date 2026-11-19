namespace RoomToRentRadar.Features.ListingManagement.Models;

/// <summary>
/// Client-side photo rules for US08. Pure data with no MAUI dependencies so the
/// rules stay unit-testable.
/// </summary>
public static class ListingPhotoRules
{
    public const long MaxPhotoBytes = 10 * 1024 * 1024;

    public const int MinPhotosOnCreate = 3;

    public const int MaxPhotosPerRequest = 10;

    public const string Jpeg = "image/jpeg";

    public const string Png = "image/png";

    private static readonly string[] JpegExtensions = [".jpg", ".jpeg"];

    private const string PngExtension = ".png";

    /// <summary>
    /// Accepts a media type (<c>image/jpeg</c>) or a file extension
    /// (<c>.png</c>, <c>jpg</c>) in a single argument.
    /// </summary>
    public static bool IsAllowedType(string? contentTypeOrExtension)
        => Normalize(contentTypeOrExtension) is not null;

    /// <summary>
    /// Accepts a media type or falls back to the file extension when the picker
    /// does not report a content type.
    /// </summary>
    public static bool IsAllowedType(string? contentType, string? fileName)
        => Normalize(contentType) is not null || FromExtension(fileName) is not null;

    /// <summary>Maps a media type or extension to a supported media type, else null.</summary>
    public static string? Normalize(string? contentTypeOrExtension)
    {
        if (string.IsNullOrWhiteSpace(contentTypeOrExtension))
            return null;

        var value = contentTypeOrExtension.Trim();

        var separator = value.IndexOf(';');
        if (separator >= 0)
            value = value[..separator].Trim();

        if (value.Equals(Jpeg, StringComparison.OrdinalIgnoreCase)
            || value.Equals("image/jpg", StringComparison.OrdinalIgnoreCase)
            || value.Equals("jpg", StringComparison.OrdinalIgnoreCase)
            || value.Equals("jpeg", StringComparison.OrdinalIgnoreCase)
            || value.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || value.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return Jpeg;
        }

        return value.Equals(Png, StringComparison.OrdinalIgnoreCase)
               || value.Equals("png", StringComparison.OrdinalIgnoreCase)
               || value.Equals(PngExtension, StringComparison.OrdinalIgnoreCase)
            ? Png
            : null;
    }

    /// <summary>Media type implied by the file name, or null when unsupported.</summary>
    public static string? FromExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var name = fileName.Trim();

        if (JpegExtensions.Any(extension => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
            return Jpeg;

        return name.EndsWith(PngExtension, StringComparison.OrdinalIgnoreCase) ? Png : null;
    }

    public static bool IsWithinSizeLimit(long? length)
        => length is not { } bytes || bytes is > 0 and <= MaxPhotoBytes;
}
