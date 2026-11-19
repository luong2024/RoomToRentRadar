using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadarUnitTests.Domain;

[TestFixture]
public class EmailAddressTests
{
    [Test]
    public void Create_WithValidEmail_NormalizesToLowercaseAndTrims()
    {
        var email = EmailAddress.Create("  DTC225180@ICTU.EDU.VN  ");

        Assert.That(email.Value, Is.EqualTo("dtc225180@ictu.edu.vn"));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("invalid-email")]
    [TestCase("missing-domain@")]
    [TestCase("@ictu.edu.vn")]
    public void Create_WithInvalidEmail_ThrowsDomainValidationException(string invalidEmail)
    {
        Assert.That(
            () => EmailAddress.Create(invalidEmail),
            Throws.TypeOf<DomainValidationException>());
    }

    [TestCase("student@ictu.edu.vn", true)]
    [TestCase("DTC12345@ICTU.EDU.VN", true)]
    [TestCase("landlord@gmail.com", false)]
    [TestCase("fake@ictu.edu.vn.com", false)]
    public void IsIctuStudentEmail_ReturnsExpectedResult(string rawEmail, bool expectedIsIctu)
    {
        var email = EmailAddress.Create(rawEmail);

        Assert.That(email.IsIctuStudentEmail(), Is.EqualTo(expectedIsIctu));
    }
}

