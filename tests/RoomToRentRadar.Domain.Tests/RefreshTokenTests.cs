using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.Tests;

public class RefreshTokenTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void Create_WhenValid_ShouldInitializeActiveRefreshToken()
    {
        var userId = Guid.NewGuid();
        var family = Guid.NewGuid();
        var expiresAt = FixedNow.AddDays(7);

        var token = RefreshToken.Create(userId, "sha256-hash", family, FixedNow, expiresAt);

        Assert.Multiple(() =>
        {
            Assert.That(token.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(token.UserId, Is.EqualTo(userId));
            Assert.That(token.TokenHash, Is.EqualTo("sha256-hash"));
            Assert.That(token.TokenFamily, Is.EqualTo(family));
            Assert.That(token.CreatedAt, Is.EqualTo(FixedNow));
            Assert.That(token.ExpiresAt, Is.EqualTo(expiresAt));
            Assert.That(token.IsRevoked, Is.False);
            Assert.That(token.IsExpired(FixedNow), Is.False);
            Assert.That(token.IsActive(FixedNow), Is.True);
            Assert.That(token.ReplacedByTokenId, Is.Null);
        });
    }

    [Test]
    public void IsExpired_WhenCurrentTimeAtOrAfterExpiry_ShouldReturnTrue()
    {
        var expiresAt = FixedNow.AddDays(7);
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", Guid.NewGuid(), FixedNow, expiresAt);

        Assert.Multiple(() =>
        {
            Assert.That(token.IsExpired(expiresAt.AddSeconds(-1)), Is.False);
            Assert.That(token.IsExpired(expiresAt), Is.True);
            Assert.That(token.IsActive(expiresAt), Is.False);
        });
    }

    [Test]
    public void Revoke_ShouldMarkTokenRevokedAndInactive()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", Guid.NewGuid(), FixedNow, FixedNow.AddDays(7));
        var revokedAt = FixedNow.AddHours(2);

        token.Revoke(revokedAt, "Logout");

        Assert.Multiple(() =>
        {
            Assert.That(token.IsRevoked, Is.True);
            Assert.That(token.RevokedAt, Is.EqualTo(revokedAt));
            Assert.That(token.RevocationReason, Is.EqualTo("Logout"));
            Assert.That(token.IsActive(revokedAt), Is.False);
        });
    }

    [Test]
    public void ReplaceWith_WhenActive_ShouldRevokeAndLinkReplacementToken()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", Guid.NewGuid(), FixedNow, FixedNow.AddDays(7));
        var replacementId = Guid.NewGuid();
        var rotatedAt = FixedNow.AddMinutes(30);

        token.ReplaceWith(replacementId, rotatedAt);

        Assert.Multiple(() =>
        {
            Assert.That(token.IsRevoked, Is.True);
            Assert.That(token.RevokedAt, Is.EqualTo(rotatedAt));
            Assert.That(token.ReplacedByTokenId, Is.EqualTo(replacementId));
            Assert.That(token.RevocationReason, Is.EqualTo("Rotated"));
            Assert.That(token.IsActive(rotatedAt), Is.False);
        });
    }

    [Test]
    public void ReplaceWith_WhenAlreadyRevokedOrExpired_ShouldThrowInvalidRefreshTokenStateException()
    {
        var expiresAt = FixedNow.AddDays(7);
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", Guid.NewGuid(), FixedNow, expiresAt);

        Assert.Throws<InvalidRefreshTokenStateException>(() =>
            token.ReplaceWith(Guid.NewGuid(), expiresAt));

        token.Revoke(FixedNow.AddMinutes(5));

        Assert.Throws<InvalidRefreshTokenStateException>(() =>
            token.ReplaceWith(Guid.NewGuid(), FixedNow.AddMinutes(10)));
    }
}
