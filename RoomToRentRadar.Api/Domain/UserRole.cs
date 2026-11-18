namespace RoomToRentRadar.Api.Domain;

public enum UserRole
{
    User,
    Moderator,
    Admin
}

/// <summary>Wire values for <see cref="UserRole"/>, also used for role claims.</summary>
public static class UserRoleWire
{
    public const string User = "user";
    public const string Moderator = "moderator";
    public const string Admin = "admin";

    public static readonly IReadOnlyList<string> All = [User, Moderator, Admin];

    public static bool TryParse(string? value, out UserRole role)
    {
        switch (value)
        {
            case User:
                role = UserRole.User;
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
        UserRole.User => User,
        UserRole.Moderator => Moderator,
        UserRole.Admin => Admin,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    public static UserRole FromWireValue(string value)
        => TryParse(value, out var role)
            ? role
            : throw new InvalidOperationException($"Unknown user role '{value}'.");
}
