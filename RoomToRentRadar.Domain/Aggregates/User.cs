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
        IEnumerable<RefreshTokenSession>? refreshTokens = null,
        string? concurrencyToken = null)
    {
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
            updatedAtUtc: utcNow);
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
            updatedAtUtc: utcNow);
    }

    public static User RegisterGoogle(
        string email,
        string fullName,
        string googleSubjectId,
        UserRole role,
        string? phoneNumber,
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
            throw new DomainValidationException("Full name is required for Google registration.");
        }

        if (string.IsNullOrWhiteSpace(googleSubjectId))
        {
            throw new DomainValidationException("Google subject identifier is required.");
        }

        var emailAddress = EmailAddress.Create(email);

        if (role == UserRole.Student)
        {
            if (!emailAddress.IsIctuStudentEmail())
            {
                throw new DomainValidationException($"Student Google login requires an '{EmailAddress.IctuDomain}' email address.");
            }

            var derivedStudentCode = emailAddress.Value.Split('@')[0].ToUpperInvariant();

            return new User(
                id: Guid.NewGuid().ToString("N"),
                email: emailAddress,
                fullName: fullName.Trim(),
                passwordHash: null,
                role: UserRole.Student,
                authProvider: AuthProvider.Google,
                ssoSubjectId: googleSubjectId.Trim(),
                phoneNumber: null,
                studentCode: derivedStudentCode,
                isVerified: true,
                failedLoginCount: 0,
                lockoutEndUtc: null,
                createdAtUtc: utcNow,
                updatedAtUtc: utcNow);
        }
        else
        {
            var normalizedPhone = NormalizeAndValidatePhone(phoneNumber, required: true);

            return new User(
                id: Guid.NewGuid().ToString("N"),
                email: emailAddress,
                fullName: fullName.Trim(),
                passwordHash: null,
                role: UserRole.Landlord,
                authProvider: AuthProvider.Google,
                ssoSubjectId: googleSubjectId.Trim(),
                phoneNumber: normalizedPhone,
                studentCode: null,
                isVerified: false, // Landlord requires Admin approval per BR-AUTH-03
                failedLoginCount: 0,
                lockoutEndUtc: null,
                createdAtUtc: utcNow,
                updatedAtUtc: utcNow);
        }
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
        string? concurrencyToken = null)
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
            refreshTokens,
            concurrencyToken);
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

    public void LinkGoogle(string googleSubjectId, string? phoneNumber, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(googleSubjectId))
        {
            throw new DomainValidationException("Google subject identifier is required.");
        }

        SsoSubjectId = googleSubjectId.Trim();
        AuthProvider = AuthProvider.Google;

        if (Role == UserRole.Landlord && string.IsNullOrWhiteSpace(PhoneNumber) && !string.IsNullOrWhiteSpace(phoneNumber))
        {
            PhoneNumber = NormalizeAndValidatePhone(phoneNumber, required: true);
        }

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
