using Microsoft.AspNetCore.Identity;
using RoomToRentRadar.Api.Auth;
using RoomToRentRadar.Api.Entities;

namespace RoomToRentRadar.Api.Data;

/// <summary>
/// Seeds Identity roles and demo accounts at startup (US01 / T01.1).
/// Safe to run on every start: each role/user is only created if it does not exist yet.
/// </summary>
public static class IdentitySeeder
{
    /// <summary>
    /// Demo accounts mirroring the MAUI seed data in RentalDatabaseService, so the whole team
    /// can log in with the same credentials. DEV ONLY: Program.cs seeds these only in Development.
    /// The password "123456" passes because Program.cs only requires length >= 6 (AC1).
    /// </summary>
    private static readonly (string Email, string FullName, string Phone, string Role)[] DemoUsers =
    [
        ("student@ictu.edu.vn", "Sinh viên ICTU (Demo)", "0988111222", AppRoles.Student),
        ("landlord@ictu.edu.vn", "Chú Hòa Chủ Trọ Z115", "0912333444", AppRoles.Landlord),
        ("admin@ictu.edu.vn", "Ban Quản Lý Nội Trú ICTU", "0909555666", AppRoles.Admin),
    ];

    private const string DemoPassword = "123456";

    public static async Task SeedAsync(IServiceProvider services, bool seedDemoUsers)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<int>>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(IdentitySeeder));

        // Roles are always required, in every environment.
        foreach (var roleName in AppRoles.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
                continue;

            var result = await roleManager.CreateAsync(new IdentityRole<int>(roleName));
            EnsureSucceeded(result, $"create role '{roleName}'", logger);
        }

        if (!seedDemoUsers)
            return;

        foreach (var demo in DemoUsers)
        {
            if (await userManager.FindByEmailAsync(demo.Email) is not null)
                continue;

            var user = new ApplicationUser
            {
                UserName = demo.Email,
                Email = demo.Email,
                EmailConfirmed = true,
                FullName = demo.FullName,
                PhoneNumber = demo.Phone,
            };

            // CreateAsync hashes the password; it is never stored in plain text.
            var created = await userManager.CreateAsync(user, DemoPassword);
            EnsureSucceeded(created, $"create demo user '{demo.Email}'", logger);

            var addedToRole = await userManager.AddToRoleAsync(user, demo.Role);
            EnsureSucceeded(addedToRole, $"add '{demo.Email}' to role '{demo.Role}'", logger);
        }
    }

    /// <summary>Logs and throws so a broken seed stops startup instead of failing silently.</summary>
    private static void EnsureSucceeded(IdentityResult result, string action, ILogger logger)
    {
        if (result.Succeeded)
            return;

        var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
        logger.LogError("Identity seeding failed to {Action}: {Errors}", action, errors);
        throw new InvalidOperationException($"Identity seeding failed to {action}: {errors}");
    }
}
