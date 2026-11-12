using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Domain.Tests;

public class UserTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [TestCase(UserRole.Student)]
    [TestCase(UserRole.Landlord)]
    public void RegisterLocalUser_WhenValidStudentOrLandlord_ShouldCreateActiveUserWithSecurityStamp(UserRole role)
    {
        var email = EmailAddress.Create("student@ictu.edu.vn");

        var user = User.RegisterLocalUser(email, "hashed-password", role, FixedNow);

        Assert.Multiple(() =>
        {
            Assert.That(user.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(user.Email, Is.EqualTo(email));
            Assert.That(user.PasswordHash, Is.EqualTo("hashed-password"));
            Assert.That(user.HasLocalPassword, Is.True);
            Assert.That(user.Role, Is.EqualTo(role));
            Assert.That(user.IsActive, Is.True);
            Assert.That(user.SecurityStamp, Is.Not.Null.And.Not.Empty);
            Assert.That(user.CreatedAt, Is.EqualTo(FixedNow));
            Assert.That(user.UpdatedAt, Is.EqualTo(FixedNow));
            Assert.That(user.LastLoginAt, Is.Null);
            Assert.That(user.ExternalLogins, Is.Empty);
        });
    }

    [Test]
    public void RegisterLocalUser_WhenRoleIsAdmin_ShouldThrowAdminSelfRegistrationForbiddenException()
    {
        var email = EmailAddress.Create("attacker@example.com");

        Assert.Throws<AdminSelfRegistrationForbiddenException>(() =>
            User.RegisterLocalUser(email, "hashed-password", UserRole.Admin, FixedNow));
    }

    [Test]
    public void RegisterLocalUser_WhenRoleIsUndefined_ShouldThrowInvalidRoleException()
    {
        var email = EmailAddress.Create("user@example.com");

        Assert.Throws<InvalidRoleException>(() =>
            User.RegisterLocalUser(email, "hashed-password", (UserRole)999, FixedNow));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void RegisterLocalUser_WhenPasswordHashEmpty_ShouldThrowInvalidUserStateException(string emptyHash)
    {
        var email = EmailAddress.Create("user@example.com");

        Assert.Throws<InvalidUserStateException>(() =>
            User.RegisterLocalUser(email, emptyHash, UserRole.Student, FixedNow));
    }

    [Test]
    public void RegisterGoogleUser_WhenValid_ShouldCreateGoogleUserWithExternalIdentityAndNoPassword()
    {
        var email = EmailAddress.Create("GoogleUser@Gmail.com");

        var user = User.RegisterGoogleUser(email, "google-sub-12345", UserRole.Landlord, FixedNow);

        Assert.Multiple(() =>
        {
            Assert.That(user.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(user.Email.Value, Is.EqualTo("googleuser@gmail.com"));
            Assert.That(user.PasswordHash, Is.Null);
            Assert.That(user.HasLocalPassword, Is.False);
            Assert.That(user.Role, Is.EqualTo(UserRole.Landlord));
            Assert.That(user.IsActive, Is.True);
            Assert.That(user.LastLoginAt, Is.EqualTo(FixedNow));
            Assert.That(user.ExternalLogins.Count, Is.EqualTo(1));

            var externalLogin = user.ExternalLogins.Single();
            Assert.That(externalLogin.UserId, Is.EqualTo(user.Id));
            Assert.That(externalLogin.Provider, Is.EqualTo(UserExternalLogin.GoogleProvider));
            Assert.That(externalLogin.ProviderSubject, Is.EqualTo("google-sub-12345"));
            Assert.That(externalLogin.ProviderEmail, Is.EqualTo(email));
        });
    }

    [Test]
    public void RegisterGoogleUser_WhenRoleIsAdmin_ShouldThrowAdminSelfRegistrationForbiddenException()
    {
        var email = EmailAddress.Create("google@example.com");

        Assert.Throws<AdminSelfRegistrationForbiddenException>(() =>
            User.RegisterGoogleUser(email, "google-sub-12345", UserRole.Admin, FixedNow));
    }

    [Test]
    public void ProvisionAdminUser_ShouldCreateActiveAdminUser()
    {
        var email = EmailAddress.Create("admin@roomtorentradar.local");

        var admin = User.ProvisionAdminUser(email, "admin-hash", FixedNow);

        Assert.Multiple(() =>
        {
            Assert.That(admin.Role, Is.EqualTo(UserRole.Admin));
            Assert.That(admin.IsActive, Is.True);
            Assert.That(admin.SecurityStamp, Is.Not.Empty);
        });
    }

    [Test]
    public void DeactivateAndActivate_ShouldToggleActiveStatusAndRotateSecurityStamp()
    {
        var user = User.RegisterLocalUser(
            EmailAddress.Create("user@example.com"),
            "hash",
            UserRole.Student,
            FixedNow);

        var initialStamp = user.SecurityStamp;
        var deactivatedAt = FixedNow.AddMinutes(10);

        user.Deactivate(deactivatedAt);

        var deactivatedStamp = user.SecurityStamp;

        Assert.Multiple(() =>
        {
            Assert.That(user.IsActive, Is.False);
            Assert.That(deactivatedStamp, Is.Not.EqualTo(initialStamp));
            Assert.That(user.UpdatedAt, Is.EqualTo(deactivatedAt));
            Assert.That(user.ValidateSecurityState(initialStamp), Is.False);
            Assert.That(user.ValidateSecurityState(deactivatedStamp), Is.False);
        });

        var reactivatedAt = FixedNow.AddMinutes(20);
        user.Activate(reactivatedAt);

        Assert.Multiple(() =>
        {
            Assert.That(user.IsActive, Is.True);
            Assert.That(user.SecurityStamp, Is.Not.EqualTo(deactivatedStamp));
            Assert.That(user.UpdatedAt, Is.EqualTo(reactivatedAt));
            Assert.That(user.ValidateSecurityState(user.SecurityStamp), Is.True);
        });
    }

    [Test]
    public void ChangePassword_ShouldUpdatePasswordHashAndRotateSecurityStamp()
    {
        var user = User.RegisterLocalUser(
            EmailAddress.Create("user@example.com"),
            "old-hash",
            UserRole.Student,
            FixedNow);

        var oldStamp = user.SecurityStamp;
        var changedAt = FixedNow.AddHours(1);

        user.ChangePassword("new-hash", changedAt);

        Assert.Multiple(() =>
        {
            Assert.That(user.PasswordHash, Is.EqualTo("new-hash"));
            Assert.That(user.SecurityStamp, Is.Not.EqualTo(oldStamp));
            Assert.That(user.UpdatedAt, Is.EqualTo(changedAt));
            Assert.That(user.ValidateSecurityState(oldStamp), Is.False);
            Assert.That(user.ValidateSecurityState(user.SecurityStamp), Is.True);
        });
    }

    [Test]
    public void RecordSuccessfulLogin_WhenInactive_ShouldThrowUserInactiveException()
    {
        var user = User.RegisterLocalUser(
            EmailAddress.Create("user@example.com"),
            "hash",
            UserRole.Student,
            FixedNow);
        user.Deactivate(FixedNow.AddMinutes(1));

        Assert.Throws<UserInactiveException>(() => user.RecordSuccessfulLogin(FixedNow.AddMinutes(2)));
    }

    [Test]
    public void ChangeRole_WhenActorIsNotAdmin_ShouldThrowInvalidRoleException()
    {
        var user = User.RegisterLocalUser(
            EmailAddress.Create("user@example.com"),
            "hash",
            UserRole.Student,
            FixedNow);

        Assert.Throws<InvalidRoleException>(() =>
            user.ChangeRole(UserRole.Landlord, UserRole.Student, FixedNow.AddMinutes(5)));
    }

    [Test]
    public void ChangeRole_WhenActorIsAdmin_ShouldUpdateRoleAndRotateSecurityStamp()
    {
        var user = User.RegisterLocalUser(
            EmailAddress.Create("user@example.com"),
            "hash",
            UserRole.Student,
            FixedNow);
        var oldStamp = user.SecurityStamp;

        user.ChangeRole(UserRole.Landlord, UserRole.Admin, FixedNow.AddMinutes(5));

        Assert.Multiple(() =>
        {
            Assert.That(user.Role, Is.EqualTo(UserRole.Landlord));
            Assert.That(user.SecurityStamp, Is.Not.EqualTo(oldStamp));
        });
    }

    [TestCase("student", UserRole.Student)]
    [TestCase("LANDLORD", UserRole.Landlord)]
    public void ParseSelfRegistrationRole_WhenValidStudentOrLandlord_ShouldReturnRole(string input, UserRole expected)
    {
        var role = UserRoleExtensions.ParseSelfRegistrationRole(input);

        Assert.That(role, Is.EqualTo(expected));
    }

    [Test]
    public void ParseSelfRegistrationRole_WhenAdmin_ShouldThrowAdminSelfRegistrationForbiddenException()
    {
        Assert.Throws<AdminSelfRegistrationForbiddenException>(() =>
            UserRoleExtensions.ParseSelfRegistrationRole("ADMIN"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("MODERATOR")]
    public void ParseSelfRegistrationRole_WhenInvalid_ShouldThrowInvalidRoleException(string? input)
    {
        Assert.Throws<InvalidRoleException>(() =>
            UserRoleExtensions.ParseSelfRegistrationRole(input));
    }
}
