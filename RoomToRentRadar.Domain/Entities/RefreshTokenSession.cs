namespace RoomToRentRadar.Domain.Entities;

public sealed class RefreshTokenSession
{
    public string TokenHash { get; private set; }
    public string JwtId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    public RefreshTokenSession(
        string tokenHash,
        string jwtId,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        DateTime? revokedAtUtc = null,
        string? replacedByTokenHash = null)
    {
        TokenHash = tokenHash;
        JwtId = jwtId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        RevokedAtUtc = revokedAtUtc;
        ReplacedByTokenHash = replacedByTokenHash;
    }

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAtUtc;

    public bool IsRevoked => RevokedAtUtc.HasValue;

    public bool IsActive(DateTime utcNow) => !IsRevoked && !IsExpired(utcNow);

    public void Revoke(DateTime utcNow, string? replacedByTokenHash = null)
    {
        if (!RevokedAtUtc.HasValue)
        {
            RevokedAtUtc = utcNow;
        }

        if (!string.IsNullOrWhiteSpace(replacedByTokenHash))
        {
            ReplacedByTokenHash = replacedByTokenHash;
        }
    }
}
