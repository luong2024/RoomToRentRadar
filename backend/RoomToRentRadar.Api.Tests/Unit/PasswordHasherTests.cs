using RoomToRentRadar.Infrastructure.Auth;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class PasswordHasherTests
{
    [Test]
    public void Hash_ProducesSaltedHashThatVerifiesOnlyTheOriginalPassword()
    {
        var hasher = new PasswordHasher();

        var (hash, salt) = hasher.Hash("Password1");

        Assert.Multiple(() =>
        {
            Assert.That(hash, Is.Not.EqualTo("Password1"));
            Assert.That(hash, Is.Not.Empty);
            Assert.That(salt, Is.Not.Empty);
            Assert.That(hasher.Verify("Password1", hash, salt), Is.True);
            Assert.That(hasher.Verify("password1", hash, salt), Is.False);
            Assert.That(hasher.Verify("Password2", hash, salt), Is.False);
        });
    }

    [Test]
    public void Hash_WithSamePassword_UsesFreshSaltAndDifferentHash()
    {
        var hasher = new PasswordHasher();

        var first = hasher.Hash("Password1");
        var second = hasher.Hash("Password1");

        Assert.Multiple(() =>
        {
            Assert.That(first.Salt, Is.Not.EqualTo(second.Salt));
            Assert.That(first.Hash, Is.Not.EqualTo(second.Hash));
            Assert.That(hasher.Verify("Password1", second.Hash, second.Salt), Is.True);
        });
    }
}
