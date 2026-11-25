using Microsoft.Extensions.Configuration;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;
using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadar.Api.Configuration;

/// <summary>
/// Loads and validates configured seed accounts (<c>Admin:SeedAccounts</c> in all environments;
/// <c>DemoData:Accounts</c> only in Development) and hashes passwords before passing them to SQLite.
/// </summary>
public static class SeedAccountsLoader
{
    public const string AdminSectionPath = "Admin:SeedAccounts";
    public const string DemoSectionPath = "DemoData:Accounts";

    public static IReadOnlyList<SeedAccount> Load(
        IConfiguration config,
        bool isDevelopment,
        IPasswordHasherPort hasher)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(hasher);

        var accounts = new List<SeedAccount>();
        var seenEmails = new HashSet<string>(StringComparer.Ordinal);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        LoadAdminAccounts(config, hasher, accounts, seenEmails, seenIds);

        if (isDevelopment)
        {
            LoadDemoAccounts(config, hasher, accounts, seenEmails, seenIds);
        }

        return accounts;
    }

    private static void LoadAdminAccounts(
        IConfiguration config,
        IPasswordHasherPort hasher,
        List<SeedAccount> accounts,
        HashSet<string> seenEmails,
        HashSet<string> seenIds)
    {
        var entries = config.GetSection(AdminSectionPath).GetChildren().ToList();
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var location = $"{AdminSectionPath}:{entry.Key}";
            var id = string.IsNullOrWhiteSpace(entry["Id"])
                ? $"seed-admin-{i + 1}"
                : entry["Id"]!.Trim();

            var email = ValidateEmail(entry["Email"], location);
            var fullName = ValidateFullName(entry["FullName"], location, email.Value);
            var passwordHash = ValidateAndHashPassword(entry["Password"], location, email.Value, hasher);

            EnsureUnique(id, email.Value, location, seenIds, seenEmails);

            accounts.Add(new SeedAccount(
                Id: id,
                Email: email.Value,
                FullName: fullName,
                Role: UserRole.Admin,
                IsVerified: true,
                PasswordHash: passwordHash));
        }
    }

    private static void LoadDemoAccounts(
        IConfiguration config,
        IPasswordHasherPort hasher,
        List<SeedAccount> accounts,
        HashSet<string> seenEmails,
        HashSet<string> seenIds)
    {
        foreach (var entry in config.GetSection(DemoSectionPath).GetChildren())
        {
            var location = $"{DemoSectionPath}:{entry.Key}";
            var rawId = entry["Id"];
            if (string.IsNullOrWhiteSpace(rawId))
            {
                throw new InvalidOperationException($"Seed account at '{location}' is missing required 'Id'.");
            }

            var id = rawId.Trim();
            var email = ValidateEmail(entry["Email"], location);
            var fullName = ValidateFullName(entry["FullName"], location, email.Value);
            var role = ParseDemoRole(entry["Role"], location, email.Value);
            var isVerified = ParseIsVerified(entry["IsVerified"], location, email.Value);
            var phoneNumber = ValidatePhone(entry["PhoneNumber"], role, location, email.Value);
            var studentCode = string.IsNullOrWhiteSpace(entry["StudentCode"])
                ? null
                : entry["StudentCode"]!.Trim().ToUpperInvariant();
            var passwordHash = ValidateAndHashPassword(entry["Password"], location, email.Value, hasher);

            EnsureUnique(id, email.Value, location, seenIds, seenEmails);

            accounts.Add(new SeedAccount(
                Id: id,
                Email: email.Value,
                FullName: fullName,
                Role: role,
                IsVerified: isVerified,
                PasswordHash: passwordHash,
                PhoneNumber: phoneNumber,
                StudentCode: studentCode));
        }
    }

    private static EmailAddress ValidateEmail(string? rawEmail, string location)
    {
        try
        {
            return EmailAddress.Create(rawEmail ?? string.Empty);
        }
        catch (DomainValidationException ex)
        {
            throw new InvalidOperationException(
                $"Seed account at '{location}' has an invalid email: {ex.Message}",
                ex);
        }
    }

    private static string ValidateFullName(string? rawFullName, string location, string email)
    {
        if (string.IsNullOrWhiteSpace(rawFullName))
        {
            throw new InvalidOperationException($"Seed account '{email}' at '{location}' is missing 'FullName'.");
        }

        var trimmed = rawFullName.Trim();
        if (trimmed.Length > 100)
        {
            throw new InvalidOperationException($"Seed account '{email}' at '{location}' exceeds 100 characters for 'FullName'.");
        }

        return trimmed;
    }

    private static UserRole ParseDemoRole(string? rawRole, string location, string email)
    {
        if (string.IsNullOrWhiteSpace(rawRole))
        {
            throw new InvalidOperationException($"Seed account '{email}' at '{location}' is missing 'Role'.");
        }

        var trimmed = rawRole.Trim();
        if (!Enum.GetNames<UserRole>().Contains(trimmed, StringComparer.Ordinal) ||
            !Enum.TryParse<UserRole>(trimmed, ignoreCase: false, out var role))
        {
            throw new InvalidOperationException(
                $"Seed account '{email}' at '{location}' has unrecognized role '{trimmed}'.");
        }

        if (role == UserRole.Admin)
        {
            throw new InvalidOperationException(
                $"Seed account '{email}' at '{location}' cannot use Role 'Admin' in '{DemoSectionPath}'. Use '{AdminSectionPath}'.");
        }

        if (role != UserRole.Student && role != UserRole.Landlord)
        {
            throw new InvalidOperationException(
                $"Seed account '{email}' at '{location}' has unsupported demo role '{role}'.");
        }

        return role;
    }

    private static bool ParseIsVerified(string? rawIsVerified, string location, string email)
    {
        if (string.IsNullOrWhiteSpace(rawIsVerified))
        {
            return false;
        }

        if (!bool.TryParse(rawIsVerified.Trim(), out var isVerified))
        {
            throw new InvalidOperationException(
                $"Seed account '{email}' at '{location}' has invalid 'IsVerified' boolean value.");
        }

        return isVerified;
    }

    private static string? ValidatePhone(string? rawPhone, UserRole role, string location, string email)
    {
        try
        {
            return User.NormalizeAndValidatePhone(rawPhone, required: role == UserRole.Landlord);
        }
        catch (DomainValidationException ex)
        {
            throw new InvalidOperationException(
                $"Seed account '{email}' at '{location}' has an invalid phone number: {ex.Message}",
                ex);
        }
    }

    private static string ValidateAndHashPassword(
        string? rawPassword,
        string location,
        string email,
        IPasswordHasherPort hasher)
    {
        if (string.IsNullOrWhiteSpace(rawPassword))
        {
            throw new InvalidOperationException(
                $"Seed account '{email}' at '{location}' is missing a password.");
        }

        try
        {
            User.ValidatePlainPasswordComplexity(rawPassword);
        }
        catch (DomainValidationException ex)
        {
            // Never include rawPassword in the exception message.
            throw new InvalidOperationException(
                $"Seed account '{email}' at '{location}' does not meet password complexity rules: {ex.Message}",
                ex);
        }

        return hasher.HashPassword(rawPassword);
    }

    private static void EnsureUnique(
        string id,
        string normalizedEmail,
        string location,
        HashSet<string> seenIds,
        HashSet<string> seenEmails)
    {
        if (!seenIds.Add(id))
        {
            throw new InvalidOperationException($"Duplicate seed account Id '{id}' at '{location}'.");
        }

        if (!seenEmails.Add(normalizedEmail))
        {
            throw new InvalidOperationException($"Duplicate seed account email '{normalizedEmail}' at '{location}'.");
        }
    }
}
