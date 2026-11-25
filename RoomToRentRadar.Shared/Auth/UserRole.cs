namespace RoomToRentRadar.Shared.Auth;

public enum UserRole
{
    Student,
    Landlord,
    Moderator,
    Admin
}

/// <summary>Wire values for <see cref="UserRole"/>, also used for role claims.</summary>
public static class UserRoleWire
{
    public const string Student = "student";
    public const string Landlord = "landlord";
    public const string Moderator = "moderator";
    public const string Admin = "admin";

    public static readonly IReadOnlyList<string> All = [Student, Landlord, Moderator, Admin];

    public static bool TryParse(string? value, out UserRole role)
    {
        switch (value)
        {
            case Student:
                role = UserRole.Student;
                return true;
            case Landlord:
                role = UserRole.Landlord;
                return true;
            case Moderator:
                role = UserRole.Moderator;
                return true;
            case Admin:
                role = UserRole.Admin;
                return true;
            default:
                role = default;
                return false;
        }
    }

    public static string ToWireValue(UserRole role) => role switch
    {
        UserRole.Student => Student,
        UserRole.Landlord => Landlord,
        UserRole.Moderator => Moderator,
        UserRole.Admin => Admin,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    public static UserRole FromWireValue(string value)
        => TryParse(value, out var role)
            ? role
            : throw new InvalidOperationException($"Unknown user role '{value}'.");
}
