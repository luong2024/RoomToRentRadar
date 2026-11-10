using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.Entities;

/// <summary>
/// Aggregate representing a persisted refresh token hash belonging to a rotation family.
/// Supports single-use token rotation, replay detection, and family revocation.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public Guid TokenFamily { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public string? RevocationReason { get; private set; }

    private RefreshToken()
    {
    }

    public static RefreshToken Create(
        Guid userId,
        string tokenHash,
        Guid tokenFamily,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (userId == Guid.Empty)
        {
            throw new InvalidRefreshTokenStateException("RefreshToken requires a valid user identifier.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new InvalidRefreshTokenStateException("RefreshToken hash is required.");
        }

        if (tokenFamily == Guid.Empty)
        {
            throw new InvalidRefreshTokenStateException("RefreshToken family identifier is required.");
        }

        if (expiresAt <= createdAt)
        {
            throw new InvalidRefreshTokenStateException("RefreshToken expiration must be strictly after creation time.");
        }

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            TokenFamily = tokenFamily,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt
        };
    }

    public bool IsExpired(DateTimeOffset utcNow) => utcNow >= ExpiresAt;

    public bool IsRevoked => RevokedAt.HasValue;

    public bool IsActive(DateTimeOffset utcNow) => !IsRevoked && !IsExpired(utcNow);

    public void Revoke(DateTimeOffset utcNow, string? reason = null)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedAt = utcNow;
        RevocationReason = string.IsNullOrWhiteSpace(reason) ? "Revoked" : reason.Trim();
    }

    public void ReplaceWith(Guid replacementTokenId, DateTimeOffset utcNow)
    {
        if (replacementTokenId == Guid.Empty || replacementTokenId == Id)
        {
            throw new InvalidRefreshTokenStateException("Replacement token identifier must be a valid distinct identifier.");
        }

        if (IsRevoked)
        {
            throw new InvalidRefreshTokenStateException("Cannot rotate an already revoked refresh token.");
        }

        if (IsExpired(utcNow))
        {
            throw new InvalidRefreshTokenStateException("Cannot rotate an expired refresh token.");
        }

        RevokedAt = utcNow;
        ReplacedByTokenId = replacementTokenId;
        RevocationReason = "Rotated";
    }
}
