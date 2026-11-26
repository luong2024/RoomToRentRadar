using RoomToRentRadar.Validations;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ProfileFormValidatorTests
{
    [TestCase("0901 234 567", "0901234567")]
    [TestCase("090-123-4567", "0901234567")]
    [TestCase("090.123.4567", "0901234567")]
    [TestCase("  0901234567  ", "0901234567")]
    [TestCase("", "")]
    [TestCase(null, "")]
    public void NormalizePhone_RemovesFormattingCharacters(string? phone, string expected)
    {
        Assert.That(ProfileFormValidator.NormalizePhone(phone), Is.EqualTo(expected));
    }

    [TestCase("0901234567", true)]
    [TestCase("0912 345 678", true)]
    [TestCase("090-123-4567", true)]
    [TestCase("090123456", false)]
    [TestCase("09012345678", false)]
    [TestCase("1901234567", false)]
    [TestCase("09012345ab", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void IsValidPhone_RequiresTenDigitsStartingWithZero(string? phone, bool expected)
    {
        Assert.That(ProfileFormValidator.IsValidPhone(phone), Is.EqualTo(expected));
    }

    [Test]
    public void IsValidBirthDate_AcceptsPastDatesOnly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ProfileFormValidator.IsValidBirthDate(new DateTime(2000, 1, 1)), Is.True);
            Assert.That(ProfileFormValidator.IsValidBirthDate(DateTime.Today.AddDays(-1)), Is.True);
            Assert.That(ProfileFormValidator.IsValidBirthDate(DateTime.Today), Is.False);
            Assert.That(ProfileFormValidator.IsValidBirthDate(DateTime.Today.AddDays(1)), Is.False);
        });
    }
}
