namespace RoomToRentRadar.Models;

public enum AppUserRole
{
    Student,
    Landlord,
    Moderator,
    Admin
}

public static class AppUserRoleWire
{
    public const string Student = "student";

    public const string Landlord = "landlord";

    public const string Moderator = "moderator";

    public const string Admin = "admin";

    public static AppUserRole? TryParse(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            Student => AppUserRole.Student,
            Landlord => AppUserRole.Landlord,
            Moderator => AppUserRole.Moderator,
            Admin => AppUserRole.Admin,
            _ => null
        };

    public static string ToWireValue(AppUserRole role)
        => role switch
        {
            AppUserRole.Student => Student,
            AppUserRole.Landlord => Landlord,
            AppUserRole.Moderator => Moderator,
            AppUserRole.Admin => Admin,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown user role.")
        };
}
