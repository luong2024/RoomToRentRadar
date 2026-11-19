using RoomToRentRadar.Infrastructure.Adapters.Security;

namespace RoomToRentRadarUnitTests.Infrastructure;

[TestFixture]
public class Pbkdf2PasswordHasherAdapterTests
{
    private Pbkdf2PasswordHasherAdapter _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new Pbkdf2PasswordHasherAdapter();
    }

    [Test]
    public void HashPassword_SamePasswordHashedTwice_ProducesDifferentHashesWithRandomSalt()
    {
        var hash1 = _sut.HashPassword("StrongPass123");
        var hash2 = _sut.HashPassword("StrongPass123");

        Assert.Multiple(() =>
        {
            Assert.That(hash1, Is.Not.EqualTo(hash2));
            Assert.That(_sut.VerifyPassword("StrongPass123", hash1), Is.True);
            Assert.That(_sut.VerifyPassword("StrongPass123", hash2), Is.True);
            Assert.That(_sut.VerifyPassword("WrongPass123", hash1), Is.False);
        });
    }

    [TestCase("")]
    [TestCase("invalid-format")]
    [TestCase("v2$100000$abc$def")]
    public void VerifyPassword_WithMalformedHash_ReturnsFalse(string malformedHash)
    {
        Assert.That(_sut.VerifyPassword("StrongPass123", malformedHash), Is.False);
    }
}

