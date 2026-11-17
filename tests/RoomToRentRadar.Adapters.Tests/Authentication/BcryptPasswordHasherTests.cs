using RoomToRentRadar.Adapters.Outbound.Authentication;
using RoomToRentRadar.Application.Exceptions;

namespace RoomToRentRadar.Adapters.Tests.Authentication;

public class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _hasher = new(workFactor: 10);

    [Test]
    public void HashAndVerifyPassword_WhenCorrectPassword_ShouldReturnTrue()
    {
        var password = "SecurePassword123!";
        var hash = _hasher.HashPassword(password);

        Assert.Multiple(() =>
        {
            Assert.That(hash, Is.Not.EqualTo(password));
            Assert.That(_hasher.VerifyPassword(password, hash), Is.True);
            Assert.That(_hasher.VerifyPassword("WrongPassword123!", hash), Is.False);
        });
    }

    [Test]
    public void HashPassword_WhenExactly72Utf8Bytes_ShouldSucceedAndDistinguishLastByte()
    {
        var password72A = new string('A', 70) + "1a";
        var password72B = new string('A', 70) + "1b";

        var hashA = _hasher.HashPassword(password72A);

        Assert.Multiple(() =>
        {
            Assert.That(_hasher.VerifyPassword(password72A, hashA), Is.True);
            Assert.That(_hasher.VerifyPassword(password72B, hashA), Is.False);
        });
    }

    [Test]
    public void HashPassword_WhenExceeds72Utf8Bytes_ShouldThrowRequestValidationExceptionInsteadOfTruncating()
    {
        var password73Bytes = new string('A', 71) + "1a";

        Assert.Throws<RequestValidationException>(() => _hasher.HashPassword(password73Bytes));
    }

    [Test]
    public void HashPassword_WhenMultiByteUtf8Exceeds72Bytes_ShouldThrowRequestValidationException()
    {
        // 25 3-byte characters = 75 UTF-8 bytes (even though character count is only 27)
        var multiBytePassword = new string('€', 25) + "1a";

        Assert.Throws<RequestValidationException>(() => _hasher.HashPassword(multiBytePassword));
    }
}
