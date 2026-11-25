using RoomToRentRadar.Models;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Validations;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class RegistrationFormValidatorTests
{
    private const string ValidPassword = "Room1234";

    [TestCase(AppUserRole.Student)]
    [TestCase(AppUserRole.Landlord)]
    public void TryValidate_WithValidForm_ReturnsTrue(AppUserRole role)
    {
        var valid = RegistrationFormValidator.TryValidate(
            "Nguyen Van A",
            "student@example.test",
            ValidPassword,
            ValidPassword,
            role,
            out var error);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(error, Is.Null);
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    public void TryValidate_WhenDisplayNameMissing_ReturnsFalse(string displayName)
    {
        var valid = RegistrationFormValidator.TryValidate(
            displayName,
            "student@example.test",
            ValidPassword,
            ValidPassword,
            AppUserRole.Student,
            out var error);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("họ tên"));
        });
    }

    [Test]
    public void TryValidate_WhenDisplayNameTooLong_ReturnsFalse()
    {
        var valid = RegistrationFormValidator.TryValidate(
            new string('a', AccountRules.DisplayNameMaxLength + 1),
            "student@example.test",
            ValidPassword,
            ValidPassword,
            AppUserRole.Student,
            out var error);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("họ tên"));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("missing-at")]
    [TestCase("@example.test")]
    [TestCase("user@")]
    public void TryValidate_WhenEmailInvalid_ReturnsFalse(string email)
    {
        var valid = RegistrationFormValidator.TryValidate(
            "Nguyen Van A",
            email,
            ValidPassword,
            ValidPassword,
            AppUserRole.Student,
            out var error);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("Email"));
        });
    }

    [Test]
    public void TryValidate_WhenEmailTooLong_ReturnsFalse()
    {
        var email = $"{new string('a', AccountRules.EmailMaxLength)}@example.test";

        var valid = RegistrationFormValidator.TryValidate(
            "Nguyen Van A",
            email,
            ValidPassword,
            ValidPassword,
            AppUserRole.Student,
            out var error);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("Email"));
        });
    }

    [TestCase("Abc123")]
    [TestCase("abcdefgh")]
    [TestCase("12345678")]
    public void TryValidate_WhenPasswordNotAlphanumericOrTooShort_ReturnsFalse(string password)
    {
        var valid = RegistrationFormValidator.TryValidate(
            "Nguyen Van A",
            "student@example.test",
            password,
            password,
            AppUserRole.Student,
            out var error);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("Mật khẩu"));
        });
    }

    [Test]
    public void TryValidate_WithMinimumPasswordLength_ReturnsTrue()
    {
        var password = new string('a', AccountRules.PasswordMinLength - 1) + "1";

        var valid = RegistrationFormValidator.TryValidate(
            "Nguyen Van A",
            "student@example.test",
            password,
            password,
            AppUserRole.Student,
            out _);

        Assert.That(valid, Is.True);
    }

    [Test]
    public void TryValidate_WhenPasswordTooLong_ReturnsFalse()
    {
        var password = new string('a', AccountRules.PasswordMaxLength) + "1";

        var valid = RegistrationFormValidator.TryValidate(
            "Nguyen Van A",
            "student@example.test",
            password,
            password,
            AppUserRole.Student,
            out var error);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("Mật khẩu"));
        });
    }

    [Test]
    public void TryValidate_WhenConfirmationMismatch_ReturnsFalse()
    {
        var valid = RegistrationFormValidator.TryValidate(
            "Nguyen Van A",
            "student@example.test",
            ValidPassword,
            "Room12345",
            AppUserRole.Student,
            out var error);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(error, Is.EqualTo("Mật khẩu nhập lại không khớp."));
        });
    }

    [TestCase(AppUserRole.Moderator)]
    [TestCase(AppUserRole.Admin)]
    public void TryValidate_WhenRoleCannotSelfRegister_ReturnsFalse(AppUserRole role)
    {
        var valid = RegistrationFormValidator.TryValidate(
            "Nguyen Van A",
            "student@example.test",
            ValidPassword,
            ValidPassword,
            role,
            out var error);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("Sinh viên hoặc Chủ phòng"));
        });
    }
}
