namespace RoomToRentRadar.Api.Auth;

/// <summary>
/// Role names used by Identity and written into the JWT "role" claim (US01 / T01.1).
/// The values MUST stay identical to RoomToRentRadar.Data.Constants.UserRoles in the MAUI app,
/// because the client compares the role string from the token against those constants.
/// </summary>
public static class AppRoles
{
    public const string Student = "Student";
    public const string Landlord = "Landlord";
    public const string Admin = "Admin";

    /// <summary>All roles, used by the seeder to create them in the database.</summary>
    public static readonly string[] All = [Student, Landlord, Admin];

    /// <summary>
    /// Roles a user may pick when self-registering. Admin is intentionally excluded:
    /// admin accounts are only created by the seeder.
    /// </summary>
    public static readonly string[] SelfRegisterable = [Student, Landlord];

    /// <summary>Case-insensitive check used by request validation.</summary>
    public static bool IsSelfRegisterable(string? role) =>
        role is not null && SelfRegisterable.Contains(role, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maps user input such as "student" to the canonical casing "Student".
    /// Returns null if the role is not allowed for self-registration.
    /// </summary>
    public static string? NormalizeSelfRegisterable(string? role) =>
        SelfRegisterable.FirstOrDefault(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
}
