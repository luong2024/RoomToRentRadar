using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;

namespace RoomToRentRadar.Api.Features.Auth;

public static class DemoUserSeeder
{
    public const string EmailKey = "Auth:DemoEmail";
    public const string PasswordKey = "Auth:DemoPassword";
    public const string DisplayNameKey = "Auth:DemoDisplayName";

    public static async Task<bool> SeedAsync(
        AppDbContext dbContext,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var email = configuration[EmailKey]?.Trim();
        var password = configuration[PasswordKey];
        var displayName = configuration[DisplayNameKey]?.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Demo user was not seeded. Set {EmailKey} and {PasswordKey} via user-secrets or environment variables to enable demo sign-in.",
                EmailKey,
                PasswordKey);
            return false;
        }

        var normalizedEmail = AuthService.NormalizeEmail(email);
        if (await dbContext.Users.AnyAsync(user => user.Email == normalizedEmail, cancellationToken))
        {
            return false;
        }

        var (hash, salt) = PasswordHasher.Hash(password);
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Demo renter" : displayName,
            PasswordHash = hash,
            PasswordSalt = salt,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded the demo user account.");
        return true;
    }
}
