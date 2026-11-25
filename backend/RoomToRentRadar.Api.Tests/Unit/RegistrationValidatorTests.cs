using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Features.Auth;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class RegistrationValidatorTests
{
    [Test]
    public void TryCreate_WithStudentCredentials_ReturnsTrimmedCommand()
    {
        var request = new RegisterRequest(
            "  Student@Example.test  ",
            "Password1",
            "  Student One  ",
            "Student");

        var valid = RegistrationValidator.TryCreate(request, out var command, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(problem, Is.Null);
            Assert.That(command.Email, Is.EqualTo("Student@Example.test"));
            Assert.That(command.Password, Is.EqualTo("Password1"));
            Assert.That(command.DisplayName, Is.EqualTo("Student One"));
            Assert.That(command.Role, Is.EqualTo(UserRole.Student));
        });
    }

    [Test]
    public void TryCreate_WithLandlordCredentials_ReturnsLandlordCommand()
    {
        var request = new RegisterRequest("landlord@example.test", "Password1", "Landlord One", "landlord");

        Assert.That(RegistrationValidator.TryCreate(request, out var command, out _), Is.True);
        Assert.That(command.Role, Is.EqualTo(UserRole.Landlord));
    }

    [TestCase("admin")]
    [TestCase("moderator")]
    [TestCase("user")]
    [TestCase("")]
    [TestCase(null)]
    public void TryCreate_WithNonSelfServiceRole_IsRejected(string? role)
    {
        var request = new RegisterRequest("user@example.test", "Password1", "Some User", role!);

        var valid = RegistrationValidator.TryCreate(request, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(CodeOf(problem), Is.EqualTo(AuthProblemFactory.InvalidRole));
        });
    }

    [TestCase("Short1")]
    [TestCase("abcdefgh")]
    [TestCase("12345678")]
    public void TryCreate_WithWeakPassword_IsRejected(string password)
    {
        var request = new RegisterRequest("user@example.test", password, "Some User", "student");

        var valid = RegistrationValidator.TryCreate(request, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(CodeOf(problem), Is.EqualTo(AuthProblemFactory.WeakPassword));
        });
    }

    [Test]
    public void TryCreate_WithTooLongPassword_IsRejected()
    {
        var password = new string('a', 100) + new string('1', 29);
        var request = new RegisterRequest("user@example.test", password, "Some User", "student");

        var valid = RegistrationValidator.TryCreate(request, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(CodeOf(problem), Is.EqualTo(AuthProblemFactory.WeakPassword));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not-an-email")]
    [TestCase("@example.test")]
    public void TryCreate_WithInvalidEmail_IsRejected(string email)
    {
        var request = new RegisterRequest(email, "Password1", "Some User", "student");

        var valid = RegistrationValidator.TryCreate(request, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(CodeOf(problem), Is.EqualTo(AuthProblemFactory.InvalidEmail));
        });
    }

    [Test]
    public void TryCreate_WithTooLongEmail_IsRejected()
    {
        var email = new string('a', Lengths.EmailLocalPartAtMax) + "@example.test";
        var request = new RegisterRequest(email, "Password1", "Some User", "student");

        var valid = RegistrationValidator.TryCreate(request, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(CodeOf(problem), Is.EqualTo(AuthProblemFactory.InvalidEmail));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    public void TryCreate_WithMissingDisplayName_IsRejected(string displayName)
    {
        var request = new RegisterRequest("user@example.test", "Password1", displayName, "student");

        var valid = RegistrationValidator.TryCreate(request, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(CodeOf(problem), Is.EqualTo(AuthProblemFactory.InvalidDisplayName));
        });
    }

    [Test]
    public void TryCreate_WithTooLongDisplayName_IsRejected()
    {
        var request = new RegisterRequest(
            "user@example.test",
            "Password1",
            new string('a', Lengths.DisplayNamePastMax),
            "student");

        var valid = RegistrationValidator.TryCreate(request, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(CodeOf(problem), Is.EqualTo(AuthProblemFactory.InvalidDisplayName));
        });
    }

    [Test]
    public void TryCreate_WithNullJsonFields_IsRejected()
    {
        var request = new RegisterRequest(null!, null!, null!, null!);

        var valid = RegistrationValidator.TryCreate(request, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(CodeOf(problem), Is.EqualTo(AuthProblemFactory.InvalidEmail));
        });
    }

    private static string? CodeOf(ProblemDetails? problem)
        => problem?.Extensions.TryGetValue("code", out var code) == true
            ? code as string
            : null;

    private static class Lengths
    {
        public const int EmailLocalPartAtMax = 200;
        public const int DisplayNamePastMax = 121;
    }
}
