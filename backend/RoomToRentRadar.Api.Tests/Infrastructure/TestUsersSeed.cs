using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.SqliteDataAccess;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Infrastructure.Auth;

namespace RoomToRentRadar.Api.Tests.Infrastructure;

public static class TestUsersSeed
{
    public const string Email = "demo@example.test";
    public const string Password = "Demo-password-123";
    public const string DisplayName = "Demo Tester";

    public static readonly Guid Id = new("aaaaaaaa-0000-0000-0000-000000000001");

    public static async Task<User> SeedAsync(AppDbContext dbContext, UserRole role = UserRole.Student)
    {
        var user = Create(Id, Email, DisplayName, Password, role);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    public static async Task<User> SeedAdditionalAsync(
        AppDbContext dbContext,
        Guid id,
        string email,
        UserRole role = UserRole.Student,
        string displayName = "Secondary Tester",
        string password = Password)
    {
        var user = Create(id, email, displayName, password, role);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    private static User Create(
        Guid id,
        string email,
        string displayName,
        string password,
        UserRole role)
    {
        var (hash, salt) = new PasswordHasher().Hash(password);
        return new User
        {
            Id = id,
            Email = email,
            DisplayName = displayName,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = role,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }
}
