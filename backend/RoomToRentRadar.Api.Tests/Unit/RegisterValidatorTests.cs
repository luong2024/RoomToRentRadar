using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Features.Auth;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class RegisterValidatorTests
{
    private const string ValidPassword = "Validpass123";

    [TestCase("student")]
    [TestCase("Landlord")]
    [TestCase(" LANDLORD ")]
    public void TryValidate_WithRegistrableRole_Succeeds(string role)
    {
        var valid = RegisterValidator.TryValidate(CreateRequest(role: role), out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(problem, Is.Null);
        });
    }

    [Test]
    public void TryValidate_WithNullRequest_ReturnsInvalidRegistrationRequest()
    {
        var valid = RegisterValidator.TryValidate(null, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(CodeOf(problem), Is.EqualTo(AuthProblemFactory.InvalidRegistrationRequest));
        });
    }

    [TestCase("not-an-email")]
    [TestCase("")]
    [TestCase(null)]
    public void TryValidate_WithInvalidEmail_ReturnsInvalidRegistrationRequest(string? email)
    {
        var valid = RegisterValidator.TryValidate(CreateRequest(email: email), out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(CodeOf(problem), Is.EqualTo(AuthProblemFactory.InvalidRegistrationRequest));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void TryValidate_WithMissingDisplayName_ReturnsInvalidRegistrationRequest(string? displayName)
    {
        var valid = RegisterValidator.TryValidate(CreateRequest(displayName: displayName), out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(CodeOf(problem), Is.EqualTo(AuthProblemFactory.InvalidRegistrationRequest));
        });
    }

    [Test]
    public void TryValidate_WithOverlongDisplayName_ReturnsInvalidRegistrationRequest()
    {
        var displayName = new string('a', AccountRules.DisplayNameMaxLength + 1);

        var valid = RegisterValidator.TryValidate(CreateRequest(displayName: displayName), out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(CodeOf(problem), Is.EqualTo(AuthProblemFactory.InvalidRegistrationRequest));
        });
    }

    [TestCase("Abc123")]
    [TestCase("abcdefgh")]
    [TestCase("12345678")]
    public void TryValidate_WithWeakPassword_ReturnsInvalidRegistrationRequest(string password)
    {
        var valid = RegisterValidator.TryValidate(CreateRequest(password: password), out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(CodeOf(problem), Is.EqualTo(AuthProblemFactory.InvalidRegistrationRequest));
        });
    }

    [TestCase(AccountRules.PasswordMinLength, true)]
    [TestCase(AccountRules.PasswordMaxLength, true)]
    [TestCase(AccountRules.PasswordMinLength - 1, false)]
    [TestCase(AccountRules.PasswordMaxLength + 1, false)]
    public void TryValidate_PasswordLengthBoundary(int length, bool expectedValid)
    {
        var password = "a1" + new string('b', length - 2);

        var valid = RegisterValidator.TryValidate(CreateRequest(password: password), out _);

        Assert.That(valid, Is.EqualTo(expectedValid));
    }

    [TestCase(UserRoleWire.Admin)]
    [TestCase(UserRoleWire.Moderator)]
    [TestCase("owner")]
    [TestCase(null)]
    public void TryValidate_WithUnsupportedRole_ReturnsUnsupportedRegistrationRole(string? role)
    {
        var valid = RegisterValidator.TryValidate(CreateRequest(role: role), out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(CodeOf(problem), Is.EqualTo(AuthProblemFactory.UnsupportedRegistrationRole));
        });
    }

    private static RegisterRequest CreateRequest(
        string? email = "new.user@example.test",
        string? password = ValidPassword,
        string? displayName = "New User",
        string? role = UserRoleWire.Student)
        => new(email!, password!, displayName!, role!);

    private static string? CodeOf(ProblemDetails? problem)
        => problem?.Extensions.TryGetValue("code", out var code) == true
            ? code as string
            : null;
}
