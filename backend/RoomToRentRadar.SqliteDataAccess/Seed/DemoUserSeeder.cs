using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Services;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.SqliteDataAccess.Seed;

public static class DemoUserSeeder
{
    public const string EmailKey = "Auth:DemoEmail";
    public const string PasswordKey = "Auth:DemoPassword";
    public const string DisplayNameKey = "Auth:DemoDisplayName";
    public const string RoleKey = "Auth:DemoRole";

    public static async Task<bool> SeedAsync(
        AppDbContext dbContext,
        IConfiguration configuration,
        IPasswordHasher passwordHasher,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var email = configuration[EmailKey]?.Trim();
        var password = configuration[PasswordKey];
        var displayName = configuration[DisplayNameKey]?.Trim();
        var role = UserRole.Student;
        var configuredRole = configuration[RoleKey]?.Trim();
        if (!string.IsNullOrEmpty(configuredRole)
            && !UserRoleWire.TryParse(configuredRole.ToLowerInvariant(), out role))
        {
            logger.LogWarning(
                "Ignoring {RoleKey} value {ConfiguredRole}; falling back to the student role.",
                RoleKey,
                configuredRole);
            role = UserRole.Student;
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Demo user was not seeded. Set {EmailKey} and {PasswordKey} via user-secrets or environment variables to enable demo sign-in.",
                EmailKey,
                PasswordKey);
            return false;
        }

        var normalizedEmail = User.NormalizeEmail(email);
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

        var (hash, salt) = passwordHasher.Hash(password);
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
