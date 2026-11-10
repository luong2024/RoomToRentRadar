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

    public bool IsExpired(DateTime utcNow) => throw new NotImplementedException();

    public bool IsRevoked => throw new NotImplementedException();

    public bool IsActive(DateTime utcNow) => throw new NotImplementedException();

    public void Revoke(DateTime utcNow, string? replacedByTokenHash = null) =>
        throw new NotImplementedException();
}

