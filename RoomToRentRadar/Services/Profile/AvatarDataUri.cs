namespace RoomToRentRadar.Services.Profile;

internal static class AvatarDataUri
{
    public static bool TryDecode(string? value, out byte[]? bytes)
    {
        bytes = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        const string jpegPrefix = "data:image/jpeg;base64,";
        const string pngPrefix = "data:image/png;base64,";
        var prefix = value.StartsWith(jpegPrefix, StringComparison.OrdinalIgnoreCase)
            ? jpegPrefix
            : value.StartsWith(pngPrefix, StringComparison.OrdinalIgnoreCase)
                ? pngPrefix
                : null;

        if (prefix is null)
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(value[prefix.Length..]);
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
