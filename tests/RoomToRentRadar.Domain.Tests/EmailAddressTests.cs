using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Domain.Tests;

public class EmailAddressTests
{
    [TestCase("Student.User@Example.COM", "student.user@example.com")]
    [TestCase("  landlord@ictu.edu.vn  ", "landlord@ictu.edu.vn")]
    [TestCase("user+tag@sub.domain.org", "user+tag@sub.domain.org")]
    public void Create_WhenValidEmail_ShouldNormalizeToTrimmedLowercase(string rawInput, string expectedNormalized)
    {
        var email = EmailAddress.Create(rawInput);

        Assert.Multiple(() =>
        {
            Assert.That(email.Value, Is.EqualTo(expectedNormalized));
            Assert.That(email.ToString(), Is.EqualTo(expectedNormalized));
        });
    }

    [Test]
    public void ValueEquality_WhenTwoEmailsDifferOnlyByCaseOrSurroundingWhitespace_ShouldBeEqual()
    {
        var email1 = EmailAddress.Create("Alice@Example.com");
        var email2 = EmailAddress.Create("  alice@example.com ");

        Assert.That(email1, Is.EqualTo(email2));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("plainaddress")]
    [TestCase("@missinglocal.com")]
    [TestCase("missingdomain@")]
    [TestCase("missingdot@domain")]
    [TestCase("two@@example.com")]
    [TestCase("user@.leadingdot.com")]
    [TestCase("user@trailingdot.com.")]
    [TestCase("user@double..dot.com")]
    [TestCase("user name@example.com")]
    public void Create_WhenInvalidFormat_ShouldThrowInvalidEmailException(string? invalidEmail)
    {
        Assert.Throws<InvalidEmailException>(() => EmailAddress.Create(invalidEmail));
    }

    [Test]
    public void Create_WhenExceedsMaxLength_ShouldThrowInvalidEmailException()
    {
        var longLocal = new string('a', 60);
        var longDomain = new string('b', 195) + ".com";
        var overLengthEmail = $"{longLocal}@{longDomain}";

        Assert.Throws<InvalidEmailException>(() => EmailAddress.Create(overLengthEmail));
    }
}
