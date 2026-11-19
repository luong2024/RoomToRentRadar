namespace RoomToRentRadar.Services.Navigation.Core;

/// <summary>
/// UI navigation guard (T00.15). This is a convenience guard only — the server remains the
/// security authority for role-gated data. Roles are the strings carried by UserProfileDto.Role
/// ("Student" | "Landlord" | "Admin"); a null/empty/unknown role is treated as Student (guest tabs
/// are identical to Student tabs).
/// </summary>
public static class NavigationPolicy
{
    /// <summary>The role a route is restricted to, or null when any role may open it.</summary>
    public static string? RequiredRole(string route) => route switch
    {
        AppRoutes.MyListings or AppRoutes.ListingEditor => "Landlord",
        AppRoutes.Moderation or AppRoutes.AdminReview or AppRoutes.AdminReport => "Admin",
        _ => null
    };

    /// <summary>True for tabs whose content is gated behind a guest sign-in prompt (BR-AUTH-07).</summary>
    public static bool RequiresSignIn(string route) =>
        route is AppRoutes.Saved or AppRoutes.Messages;

    /// <summary>Whether <paramref name="role"/> may open <paramref name="route"/> (UI guard).</summary>
    public static bool CanAccess(string? role, string route)
    {
        var required = RequiredRole(route);
        if (required is null)
        {
            return true;
        }

        return string.Equals(NormalizeRole(role), required, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Maps a raw role string to a canonical role; guests and unknown roles become Student.</summary>
    internal static string NormalizeRole(string? role)
    {
        var trimmed = role?.Trim();
        if (string.Equals(trimmed, "Landlord", StringComparison.OrdinalIgnoreCase))
        {
            return "Landlord";
        }

        if (string.Equals(trimmed, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return "Admin";
        }

        return "Student";
    }
}