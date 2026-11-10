using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Api.Authorization;

public static class AuthorizationPolicies
{
    public const string StudentPolicy = "StudentPolicy";
    public const string LandlordPolicy = "LandlordPolicy";
    public const string VerifiedLandlordPolicy = "VerifiedLandlordPolicy";
    public const string AdminPolicy = "AdminPolicy";
    public const string VerifiedClaimType = "is_verified";

    public static void ConfigurePolicies(AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(StudentPolicy, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireRole(nameof(UserRole.Student), nameof(UserRole.Admin)));

        options.AddPolicy(LandlordPolicy, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireRole(nameof(UserRole.Landlord), nameof(UserRole.Admin)));

        options.AddPolicy(VerifiedLandlordPolicy, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireAssertion(ctx => CanPublishListing(ctx.User)));

        options.AddPolicy(AdminPolicy, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireRole(nameof(UserRole.Admin)));
    }

    public static bool IsAuthenticatedUser(this ClaimsPrincipal? principal) =>
        principal?.Identity?.IsAuthenticated == true;

    public static string GetRequiredUserId(this ClaimsPrincipal? principal)
    {
        if (!principal.IsAuthenticatedUser())
        {
            throw new InvalidTokenException("User is not authenticated.");
        }

        var userId = principal!.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal!.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidTokenException("Authenticated token does not contain a subject identifier.");
        }

        return userId;
    }

    public static bool HasRole(this ClaimsPrincipal? principal, UserRole role)
    {
        if (!principal.IsAuthenticatedUser())
        {
            return false;
        }

        var roleName = role.ToString();
        return principal!.IsInRole(roleName) ||
            principal.Claims.Any(c =>
                (string.Equals(c.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(c.Type, "role", StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(c.Value, roleName, StringComparison.OrdinalIgnoreCase));
    }

    public static bool CanPublishListing(this ClaimsPrincipal? principal)
    {
        if (!principal.IsAuthenticatedUser())
        {
            return false;
        }

        if (principal.HasRole(UserRole.Admin))
        {
            return true;
        }

        if (!principal.HasRole(UserRole.Landlord))
        {
            return false;
        }

        var isVerified = principal!.FindFirstValue(VerifiedClaimType);
        return string.Equals(isVerified, "true", StringComparison.OrdinalIgnoreCase);
    }

    public static string? MaskLandlordPhoneIfAnonymous(this ClaimsPrincipal? principal, string? landlordPhoneNumber) =>
        principal.IsAuthenticatedUser() ? landlordPhoneNumber : null;
}
