using RoomToRentRadar.Shared.Auth;

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
    public const string Student = UserRoleWire.Student;

    public const string Landlord = UserRoleWire.Landlord;

    public const string Moderator = UserRoleWire.Moderator;

    public const string Admin = UserRoleWire.Admin;

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
