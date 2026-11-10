using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Domain.Aggregates;

public sealed class User
{
    public const int MaxFailedLoginAttempts = 5;
    public static readonly TimeSpan DefaultLockoutDuration = TimeSpan.FromMinutes(15);
    public const int MaxStoredRefreshTokens = 10;

    private readonly List<RefreshTokenSession> _refreshTokens = new();

    public string Id { get; private set; } = string.Empty;
    public EmailAddress Email { get; private set; } = null!;
    public string FullName { get; private set; } = string.Empty;
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

    public IReadOnlyList<RefreshTokenSession> RefreshTokens => _refreshTokens.AsReadOnly();

    private User()
    {
    }

    public static User RegisterLocal(
        string email,
        string passwordHash,
        string fullName,
        UserRole role,
        string? phoneNumber,
        string? studentCode,
        DateTime utcNow) => throw new NotImplementedException();

    public static User RegisterIctuSso(
        string email,
        string fullName,
        string ssoSubjectId,
        string? studentCode,
        DateTime utcNow) => throw new NotImplementedException();

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
        IEnumerable<RefreshTokenSession>? refreshTokens = null) => throw new NotImplementedException();

    public bool IsLockedOut(DateTime utcNow) => throw new NotImplementedException();

    public void EnsureNotLockedOut(DateTime utcNow) => throw new NotImplementedException();

    public void RecordLoginSuccess(DateTime utcNow) => throw new NotImplementedException();

    public void RecordLoginFailure(DateTime utcNow) => throw new NotImplementedException();

    public void LinkIctuSso(string ssoSubjectId, string? studentCode, DateTime utcNow) =>
        throw new NotImplementedException();

    public void ApproveLandlord(DateTime utcNow) => throw new NotImplementedException();

    public void EnsureCanPublishListing() => throw new NotImplementedException();

    public RefreshTokenSession IssueRefreshToken(
        string tokenHash,
        string jwtId,
        DateTime utcNow,
        TimeSpan lifetime) => throw new NotImplementedException();

    public RefreshTokenSession RotateRefreshToken(
        string currentTokenHash,
        string newTokenHash,
        string newJwtId,
        DateTime utcNow,
        TimeSpan lifetime) => throw new NotImplementedException();

    public void RevokeRefreshToken(string tokenHash, DateTime utcNow) =>
        throw new NotImplementedException();

    public void RevokeAllRefreshTokens(DateTime utcNow) =>
        throw new NotImplementedException();

    public static void ValidatePlainPasswordComplexity(string password) =>
        throw new NotImplementedException();

    public static string? NormalizeAndValidatePhone(string? phoneNumber, bool required) =>
        throw new NotImplementedException();
}

