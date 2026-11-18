using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;

namespace RoomToRentRadar.Api.Features.Auth;

public static class DemoUserSeeder
{
    public const string EmailKey = "Auth:DemoEmail";
    public const string PasswordKey = "Auth:DemoPassword";
    public const string DisplayNameKey = "Auth:DemoDisplayName";
    public const string RoleKey = "Auth:DemoRole";

    public static async Task<bool> SeedAsync(
        AppDbContext dbContext,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var email = configuration[EmailKey]?.Trim();
        var password = configuration[PasswordKey];
        var displayName = configuration[DisplayNameKey]?.Trim();
        var role = UserRole.User;
        var configuredRole = configuration[RoleKey]?.Trim();
        if (!string.IsNullOrEmpty(configuredRole)
            && !UserRoleWire.TryParse(configuredRole.ToLowerInvariant(), out role))
        {
            logger.LogWarning(
                "Ignoring {RoleKey} value {ConfiguredRole}; falling back to the user role.",
                RoleKey,
                configuredRole);
            role = UserRole.User;
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Demo user was not seeded. Set {EmailKey} and {PasswordKey} via user-secrets or environment variables to enable demo sign-in.",
                EmailKey,
                PasswordKey);
            return false;
        }

        var normalizedEmail = AuthService.NormalizeEmail(email);
        var existing = await dbContext.Users
            .FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            if (existing.Role == role)
            {
                return false;
            }

            existing.Role = role;
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Updated the demo user role to {Role}.", UserRoleWire.ToWireValue(role));
            return true;
        }

        var (hash, salt) = PasswordHasher.Hash(password);
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Demo renter" : displayName,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = role,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded the demo user account.");
        return true;
    }
}
