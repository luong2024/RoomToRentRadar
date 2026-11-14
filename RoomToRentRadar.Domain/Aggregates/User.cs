using System.Text.RegularExpressions;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Domain.Aggregates;

public sealed class User
{
    public const int MaxFailedLoginAttempts = 5;
    public static readonly TimeSpan DefaultLockoutDuration = TimeSpan.FromMinutes(15);
    public const int MaxStoredRefreshTokens = 10;

    // BR-CMP-02 / BR-I18N-02: defaults mirror migration 0002 (Users.SelectedCampusId / PreferredLanguage).
    public const string DefaultSelectedCampusId = "ICTU";
    public const string DefaultPreferredLanguage = "en";
    public static readonly IReadOnlyList<string> SupportedLanguages = new[] { "en", "vi" };

    private static readonly Regex VietnamPhoneRegex = new(
        @"^(0|\+84)[0-9]{9}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly List<RefreshTokenSession> _refreshTokens = new();

    public string Id { get; private set; }
    public EmailAddress Email { get; private set; }
    public string FullName { get; private set; }
    public string? PasswordHash { get; private set; }
    public UserRole Role { get; private set; }
    public AuthProvider AuthProvider { get; private set; }
    public string? SsoSubjectId { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? StudentCode { get; private set; }
    public bool IsVerified { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTime? LockoutEndUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    // BR-CMP-02: campus the user searches around; defaults to ICTU.
    public string SelectedCampusId { get; private set; }
    // BR-I18N-02: preferred UI language, restricted to SupportedLanguages.
    public string PreferredLanguage { get; private set; }
    // GUARDRAILS 1.5.2: optimistic concurrency version, starts at 1.
    public int Version { get; private set; }
    public string? ConcurrencyToken { get; private set; }

    public IReadOnlyList<RefreshTokenSession> RefreshTokens => _refreshTokens.AsReadOnly();

    private User(
        string id,
        EmailAddress email,
        string fullName,
        string? passwordHash,
        UserRole role,
        AuthProvider authProvider,
        string? ssoSubjectId,
        string? phoneNumber,
        string? studentCode,
        bool isVerified,
        int failedLoginCount,
        DateTime? lockoutEndUtc,
        DateTime createdAtUtc,
        DateTime updatedAtUtc,
        string selectedCampusId,
        string preferredLanguage,
        int version,
        IEnumerable<RefreshTokenSession>? refreshTokens = null,
        string? concurrencyToken = null)
    {
        // BR-CMP-02
        if (string.IsNullOrWhiteSpace(selectedCampusId))
        {
            throw new DomainValidationException("Selected campus is required.");
        }

        // BR-I18N-02: ordinal, case-sensitive match ('EN', 'Vi', 'fr' are rejected).
        if (preferredLanguage is null || !SupportedLanguages.Contains(preferredLanguage, StringComparer.Ordinal))
        {
            throw new DomainValidationException($"Preferred language '{preferredLanguage}' is not supported. Supported values: en, vi.");
        }

        // GUARDRAILS 1.5.2
        if (version < 1)
        {
            throw new DomainValidationException("Version must be greater than or equal to 1.");
        }

        Id = id;
        Email = email;
        FullName = fullName;
        PasswordHash = passwordHash;
        Role = role;
        AuthProvider = authProvider;
        SsoSubjectId = ssoSubjectId;
        PhoneNumber = phoneNumber;
        StudentCode = studentCode;
        IsVerified = isVerified;
        FailedLoginCount = failedLoginCount;
        LockoutEndUtc = lockoutEndUtc;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        SelectedCampusId = selectedCampusId;
        PreferredLanguage = preferredLanguage;
        Version = version;
        ConcurrencyToken = concurrencyToken;

        if (refreshTokens != null)
        {
            _refreshTokens.AddRange(refreshTokens);
        }
    }

    public static User RegisterLocal(
        string email,
        string passwordHash,
        string fullName,
        UserRole role,
        string? phoneNumber,
        string? studentCode,
        DateTime utcNow)
    {
        if (!Enum.IsDefined(typeof(UserRole), role))
        {
            throw new DomainValidationException($"Role '{(int)role}' is not a recognized role.");
        }

        if (role != UserRole.Student && role != UserRole.Landlord)
        {
            throw new ForbiddenRoleException("Public self-registration is only permitted for Student and Landlord roles.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainValidationException("Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainValidationException("Password hash is required.");
        }

        var emailAddress = EmailAddress.Create(email);
        var normalizedPhone = NormalizeAndValidatePhone(phoneNumber, required: role == UserRole.Landlord);
        var normalizedStudentCode = string.IsNullOrWhiteSpace(studentCode) ? null : studentCode.Trim().ToUpperInvariant();

        // Student is verified immediately; Landlord requires Admin approval before publishing rooms.
        var isVerified = role == UserRole.Student;

        return new User(
            id: Guid.NewGuid().ToString("N"),
            email: emailAddress,
            fullName: fullName.Trim(),
            passwordHash: passwordHash,
            role: role,
            authProvider: AuthProvider.Local,
            ssoSubjectId: null,
            phoneNumber: normalizedPhone,
            studentCode: normalizedStudentCode,
            isVerified: isVerified,
            failedLoginCount: 0,
            lockoutEndUtc: null,
            createdAtUtc: utcNow,
            updatedAtUtc: utcNow,
            selectedCampusId: DefaultSelectedCampusId,
            preferredLanguage: DefaultPreferredLanguage,
            version: 1);
    }

    public static User RegisterIctuSso(
        string email,
        string fullName,
        string ssoSubjectId,
        string? studentCode,
        DateTime utcNow)
    {
        var emailAddress = EmailAddress.Create(email);
        if (!emailAddress.IsIctuStudentEmail())
        {
            throw new DomainValidationException($"ICTU Student SSO requires an '{EmailAddress.IctuDomain}' email address.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainValidationException("Full name is required for ICTU SSO registration.");
        }

        if (string.IsNullOrWhiteSpace(ssoSubjectId))
        {
            throw new DomainValidationException("SSO subject identifier is required.");
        }

        var derivedStudentCode = string.IsNullOrWhiteSpace(studentCode)
            ? emailAddress.Value.Split('@')[0].ToUpperInvariant()
            : studentCode.Trim().ToUpperInvariant();

        return new User(
            id: Guid.NewGuid().ToString("N"),
            email: emailAddress,
            fullName: fullName.Trim(),
            passwordHash: null,
            role: UserRole.Student,
            authProvider: AuthProvider.IctuSso,
            ssoSubjectId: ssoSubjectId.Trim(),
            phoneNumber: null,
            studentCode: derivedStudentCode,
            isVerified: true,
            failedLoginCount: 0,
            lockoutEndUtc: null,
            createdAtUtc: utcNow,
            updatedAtUtc: utcNow,
            selectedCampusId: DefaultSelectedCampusId,
            preferredLanguage: DefaultPreferredLanguage,
            version: 1);
    }

    public static User Reconstitute(
        string id,
        string email,
        string fullName,
        string? passwordHash,
        UserRole role,
        AuthProvider authProvider,
        string? ssoSubjectId,
        string? phoneNumber,
        string? studentCode,
        bool isVerified,
        int failedLoginCount,
        DateTime? lockoutEndUtc,
        DateTime createdAtUtc,
        DateTime updatedAtUtc,
        IEnumerable<RefreshTokenSession>? refreshTokens = null,
        string? concurrencyToken = null,
        string selectedCampusId = DefaultSelectedCampusId,
        string preferredLanguage = DefaultPreferredLanguage,
        int version = 1)
    {
        return new User(
            id,
            EmailAddress.Create(email),
            fullName,
            passwordHash,
            role,
            authProvider,
            ssoSubjectId,
            phoneNumber,
            studentCode,
            isVerified,
            failedLoginCount,
            lockoutEndUtc,
            createdAtUtc,
            updatedAtUtc,
            selectedCampusId: selectedCampusId,
            preferredLanguage: preferredLanguage,
            version: version,
            refreshTokens: refreshTokens,
            concurrencyToken: concurrencyToken);
    }

    public void SetConcurrencyToken(string? token)
    {
        ConcurrencyToken = token;
    }

    public bool IsLockedOut(DateTime utcNow) =>
        LockoutEndUtc.HasValue && LockoutEndUtc.Value > utcNow;

    public void EnsureNotLockedOut(DateTime utcNow)
    {
        if (IsLockedOut(utcNow))
        {
            throw new AccountLockedException(LockoutEndUtc!.Value);
        }
    }

    public void RecordLoginSuccess(DateTime utcNow)
    {
        FailedLoginCount = 0;
        LockoutEndUtc = null;
        UpdatedAtUtc = utcNow;
    }

    public void RecordLoginFailure(DateTime utcNow)
    {
        if (LockoutEndUtc.HasValue && LockoutEndUtc.Value <= utcNow)
        {
            FailedLoginCount = 0;
            LockoutEndUtc = null;
        }

        FailedLoginCount++;
        if (FailedLoginCount >= MaxFailedLoginAttempts)
        {
            LockoutEndUtc = utcNow.Add(DefaultLockoutDuration);
        }

        UpdatedAtUtc = utcNow;
    }

    public void LinkIctuSso(string ssoSubjectId, string? studentCode, DateTime utcNow)
    {
        if (!Email.IsIctuStudentEmail())
        {
            throw new DomainValidationException($"Only '{EmailAddress.IctuDomain}' accounts can link ICTU SSO.");
        }

        if (string.IsNullOrWhiteSpace(ssoSubjectId))
        {
            throw new DomainValidationException("SSO subject identifier is required.");
        }

        SsoSubjectId = ssoSubjectId.Trim();
        if (!string.IsNullOrWhiteSpace(studentCode))
        {
            StudentCode = studentCode.Trim().ToUpperInvariant();
        }

        IsVerified = true;
        UpdatedAtUtc = utcNow;
    }

    public void ApproveLandlord(DateTime utcNow)
    {
        if (Role != UserRole.Landlord)
        {
            throw new DomainValidationException("Only accounts with the Landlord role require Landlord approval.");
        }

        IsVerified = true;
        UpdatedAtUtc = utcNow;
    }

    public void EnsureCanPublishListing()
    {
        if (Role != UserRole.Landlord && Role != UserRole.Admin)
        {
            throw new ForbiddenRoleException("Only Landlord or Admin accounts are permitted to publish room listings.");
        }

        if (Role == UserRole.Landlord && !IsVerified)
        {
            throw new LandlordNotApprovedException(Id);
        }
    }

    public RefreshTokenSession IssueRefreshToken(
        string tokenHash,
        string jwtId,
        DateTime utcNow,
        TimeSpan lifetime)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainValidationException("Refresh token hash is required.");
        }

        if (string.IsNullOrWhiteSpace(jwtId))
        {
            throw new DomainValidationException("JWT ID is required.");
        }

        var session = new RefreshTokenSession(
            tokenHash: tokenHash,
            jwtId: jwtId,
            createdAtUtc: utcNow,
            expiresAtUtc: utcNow.Add(lifetime));

        _refreshTokens.Add(session);
        TrimRefreshTokenHistory();
        UpdatedAtUtc = utcNow;
        return session;
    }

    public RefreshTokenSession RotateRefreshToken(
        string currentTokenHash,
        string newTokenHash,
        string newJwtId,
        DateTime utcNow,
        TimeSpan lifetime)
    {
        var existing = _refreshTokens.FirstOrDefault(t => t.TokenHash == currentTokenHash);
        if (existing == null)
        {
            throw new InvalidTokenException("Refresh token does not exist.");
        }

        if (existing.IsRevoked)
        {
            RevokeAllRefreshTokens(utcNow);
            throw new InvalidTokenException("Refresh token has already been revoked.");
        }

        if (existing.IsExpired(utcNow))
        {
            existing.Revoke(utcNow);
            throw new InvalidTokenException("Refresh token has expired.");
        }

        existing.Revoke(utcNow, replacedByTokenHash: newTokenHash);
        return IssueRefreshToken(newTokenHash, newJwtId, utcNow, lifetime);
    }

    public void RevokeRefreshToken(string tokenHash, DateTime utcNow)
    {
        var existing = _refreshTokens.FirstOrDefault(t => t.TokenHash == tokenHash);
        if (existing == null || !existing.IsActive(utcNow))
        {
            throw new InvalidTokenException("Refresh token is invalid or already inactive.");
        }

        existing.Revoke(utcNow);
        UpdatedAtUtc = utcNow;
    }

    public void RevokeAllRefreshTokens(DateTime utcNow)
    {
        foreach (var token in _refreshTokens.Where(t => !t.IsRevoked))
        {
            token.Revoke(utcNow);
        }

        UpdatedAtUtc = utcNow;
    }

    public static void ValidatePlainPasswordComplexity(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8 || password.Length > 128)
        {
            throw new DomainValidationException("Password must be between 8 and 128 characters long.");
        }

        var hasUpper = password.Any(char.IsUpper);
        var hasLower = password.Any(char.IsLower);
        var hasDigit = password.Any(char.IsDigit);

        if (!hasUpper || !hasLower || !hasDigit)
        {
            throw new DomainValidationException("Password must contain at least one uppercase letter, one lowercase letter, and one digit.");
        }
    }

    public static string? NormalizeAndValidatePhone(string? phoneNumber, bool required)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            if (required)
            {
                throw new DomainValidationException("A valid phone number is required for Landlord registration.");
            }

            return null;
        }

        var normalized = phoneNumber.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
        if (!VietnamPhoneRegex.IsMatch(normalized))
        {
            throw new DomainValidationException($"'{phoneNumber}' is not a valid phone number.");
        }

        return normalized;
    }

    private void TrimRefreshTokenHistory()
    {
        while (_refreshTokens.Count > MaxStoredRefreshTokens)
        {
            var oldest = _refreshTokens
                .OrderBy(t => t.IsActive(DateTime.UtcNow) ? 1 : 0)
                .ThenBy(t => t.CreatedAtUtc)
                .First();
            _refreshTokens.Remove(oldest);
        }
    }
}
