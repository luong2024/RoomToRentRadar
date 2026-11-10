using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Domain.Entities;

/// <summary>
/// Aggregate Root representing an authenticated platform user and their identity state.
/// Protects role invariants, active/inactive lifecycle, security stamp rotation, and external identity bindings.
/// </summary>
public sealed class User
{
    private readonly List<UserExternalLogin> _externalLogins = [];

    public Guid Id { get; private set; }

    public EmailAddress Email { get; private set; } = null!;

    public string? PasswordHash { get; private set; }

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public string SecurityStamp { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public IReadOnlyCollection<UserExternalLogin> ExternalLogins => _externalLogins.AsReadOnly();

    private User()
    {
    }

    public static User RegisterLocalUser(
        EmailAddress email,
        string passwordHash,
        UserRole role,
        DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(email);

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new InvalidUserStateException("Password hash is required for local user registration.");
        }

        UserRoleExtensions.EnsureSelfRegistrable(role);

        return new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true,
            SecurityStamp = GenerateSecurityStamp(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public static User RegisterGoogleUser(
        EmailAddress email,
        string providerSubject,
        UserRole role,
        DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(email);

        if (string.IsNullOrWhiteSpace(providerSubject))
        {
            throw new InvalidUserStateException("Google subject identifier is required.");
        }

        UserRoleExtensions.EnsureSelfRegistrable(role);

        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = email,
            PasswordHash = null,
            Role = role,
            IsActive = true,
            SecurityStamp = GenerateSecurityStamp(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            LastLoginAt = utcNow
        };

        user._externalLogins.Add(
            UserExternalLogin.Create(
                userId,
                UserExternalLogin.GoogleProvider,
                providerSubject,
                email,
                utcNow));

        return user;
    }

    /// <summary>
    /// Server-side administrative provisioning method. Never exposed to public registration endpoints.
    /// </summary>
    public static User ProvisionAdminUser(
        EmailAddress email,
        string passwordHash,
        DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(email);

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new InvalidUserStateException("Password hash is required for administrator provisioning.");
        }

        return new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHash,
            Role = UserRole.Admin,
            IsActive = true,
            SecurityStamp = GenerateSecurityStamp(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public bool HasLocalPassword => !string.IsNullOrWhiteSpace(PasswordHash);

    public void EnsureActive()
    {
        if (!IsActive)
        {
            throw new UserInactiveException();
        }
    }

    public void RecordSuccessfulLogin(DateTimeOffset utcNow)
    {
        EnsureActive();
        LastLoginAt = utcNow;
        UpdatedAt = utcNow;
    }

    public void Deactivate(DateTimeOffset utcNow)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        SecurityStamp = GenerateSecurityStamp();
        UpdatedAt = utcNow;
    }

    public void Activate(DateTimeOffset utcNow)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        SecurityStamp = GenerateSecurityStamp();
        UpdatedAt = utcNow;
    }

    public void ChangePassword(string newPasswordHash, DateTimeOffset utcNow)
    {
        EnsureActive();

        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            throw new InvalidUserStateException("New password hash cannot be empty.");
        }

        PasswordHash = newPasswordHash;
        SecurityStamp = GenerateSecurityStamp();
        UpdatedAt = utcNow;
    }

    public void ChangeRole(UserRole newRole, UserRole actorRole, DateTimeOffset utcNow)
    {
        if (actorRole != UserRole.Admin)
        {
            throw new InvalidRoleException("Only an ADMIN actor is permitted to change a user's role.");
        }

        if (!Enum.IsDefined(newRole))
        {
            throw new InvalidRoleException($"Role '{newRole}' is not defined.");
        }

        if (Role == newRole)
        {
            return;
        }

        Role = newRole;
        SecurityStamp = GenerateSecurityStamp();
        UpdatedAt = utcNow;
    }

    public bool ValidateSecurityState(string? tokenSecurityStamp)
    {
        return IsActive &&
               !string.IsNullOrWhiteSpace(tokenSecurityStamp) &&
               string.Equals(SecurityStamp, tokenSecurityStamp, StringComparison.Ordinal);
    }

    private static string GenerateSecurityStamp() => Guid.NewGuid().ToString("N");
}
