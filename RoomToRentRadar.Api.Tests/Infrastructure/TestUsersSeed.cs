using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Api.Features.Auth;

namespace RoomToRentRadar.Api.Tests.Infrastructure;

public static class TestUsersSeed
{
    public const string Email = "demo@example.test";
    public const string Password = "Demo-password-123";
    public const string DisplayName = "Demo Tester";

    public static async Task<User> SeedAsync(AppDbContext dbContext)
    {
        var (hash, salt) = PasswordHasher.Hash(Password);
        var user = new User
        {
            Id = new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
            Email = Email,
            DisplayName = DisplayName,
            PasswordHash = hash,
            PasswordSalt = salt,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }
}
