using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.Enums;

/// <summary>
/// Supported authorization roles within RoomToRentRadar.
/// </summary>
public enum UserRole
{
    Student = 1,
    Landlord = 2,
    Admin = 3
}

public static class UserRoleExtensions
{
    public const string StudentRoleName = "STUDENT";
    public const string LandlordRoleName = "LANDLORD";
    public const string AdminRoleName = "ADMIN";

    public static string ToRoleName(this UserRole role) => role switch
    {
        UserRole.Student => StudentRoleName,
        UserRole.Landlord => LandlordRoleName,
        UserRole.Admin => AdminRoleName,
        _ => throw new InvalidRoleException($"Unsupported user role value '{role}'.")
    };

    public static UserRole ParseRole(string? roleText)
    {
        if (string.IsNullOrWhiteSpace(roleText))
        {
            throw new InvalidRoleException("Role is required.");
        }

        var normalized = roleText.Trim().ToUpperInvariant();
        return normalized switch
        {
            StudentRoleName => UserRole.Student,
            LandlordRoleName => UserRole.Landlord,
            AdminRoleName => UserRole.Admin,
            _ => throw new InvalidRoleException($"Role '{roleText}' is not valid. Allowed self-registration roles are STUDENT and LANDLORD.")
        };
    }

    public static UserRole ParseSelfRegistrationRole(string? roleText)
    {
        var role = ParseRole(roleText);
        EnsureSelfRegistrable(role);
        return role;
    }

    public static void EnsureSelfRegistrable(UserRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new InvalidRoleException($"Role '{role}' is not defined.");
        }

        if (role == UserRole.Admin)
        {
            throw new AdminSelfRegistrationForbiddenException();
        }
    }
}
