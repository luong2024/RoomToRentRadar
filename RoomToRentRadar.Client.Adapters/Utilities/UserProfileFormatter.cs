namespace RoomToRentRadar.Client.Adapters.Utilities;

public static class UserProfileFormatter
{
    public static string ComputeInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "ST";

        name = name.Trim();

        // If email format, extract username parts
        if (name.Contains('@'))
        {
            var localPart = name.Split('@')[0];
            var parts = localPart.Split(new[] { '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
            }
            return localPart.Length >= 2
                ? localPart[..2].ToUpperInvariant()
                : localPart.ToUpperInvariant();
        }

        // Standard name: split by spaces (e.g. "Lê Minh" -> "LM")
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
        {
            return $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[^1][0])}";
        }

        return name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();
    }

    public static string FormatRoleBadge(string? role, bool isVerified, string? authProvider = null, string? language = null)
    {
        bool isVi = !string.IsNullOrWhiteSpace(language)
            ? language.StartsWith("vi", StringComparison.OrdinalIgnoreCase)
            : System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(role))
            return isVi ? "Sinh viên · đăng nhập qua ICTU" : "Student · signed in with ICTU";

        var trimmedRole = role.Trim();
        if (trimmedRole.Equals("Landlord", StringComparison.OrdinalIgnoreCase))
        {
            if (isVi)
                return isVerified ? "Chủ trọ · đã xác minh" : "Chủ trọ · chờ xác minh";
            return isVerified ? "Landlord · verified" : "Landlord · pending verification";
        }

        if (trimmedRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
        {
            bool isIctu = string.Equals(authProvider, "IctuSso", StringComparison.OrdinalIgnoreCase);
            if (isVi)
                return isIctu ? "Sinh viên · đăng nhập qua ICTU" : "Sinh viên · đã xác minh email";
            return isIctu ? "Student · signed in with ICTU" : "Student · verified email";
        }

        if (trimmedRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            return isVi ? "Quản trị viên · ICTU Radar" : "Administrator · ICTU Radar";
        }

        return isVi ? $"{trimmedRole} · đăng nhập qua ICTU" : $"{trimmedRole} · signed in with ICTU";
    }
}
