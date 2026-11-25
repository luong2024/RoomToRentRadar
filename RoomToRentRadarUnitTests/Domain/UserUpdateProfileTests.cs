using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadarUnitTests.Domain;

/// <summary>T13.4 — <see cref="User.UpdateProfile"/> (BR-CMP-02, BR-I18N-02, GUARDRAILS 1.5.1).</summary>
[TestFixture]
public class UserUpdateProfileTests
{
    private static readonly DateTime CreatedAt = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = CreatedAt.AddDays(1);

    private static User CreateLandlord() => User.RegisterLocal(
        "landlord@example.com",
        "hash",
        "Tran Van Chu Tro",
        UserRole.Landlord,
        "0987654321",
        null,
        CreatedAt);

    [Test]
    public void UpdateProfile_ProvidedFieldsOnly_ChangesThemAndTouchesUpdatedAt()
    {
        var user = CreateLandlord();

        var changed = user.UpdateProfile("  New Name  ", null, "TNUT", null, Later);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(user.FullName, Is.EqualTo("New Name"));
            Assert.That(user.SelectedCampusId, Is.EqualTo("TNUT"));
            Assert.That(user.PhoneNumber, Is.EqualTo("0987654321"));
            Assert.That(user.PreferredLanguage, Is.EqualTo("en"));
            Assert.That(user.UpdatedAtUtc, Is.EqualTo(Later));
        });
    }

    [Test]
    public void UpdateProfile_AllNull_ReturnsFalse_ChangesNothing()
    {
        var user = CreateLandlord();

        var changed = user.UpdateProfile(null, null, null, null, Later);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(user.UpdatedAtUtc, Is.EqualTo(CreatedAt));
            Assert.That(user.SelectedCampusId, Is.EqualTo("ICTU"));
        });
    }

    [Test]
    public void UpdateProfile_SameValues_ReturnsFalse()
    {
        var user = CreateLandlord();

        var changed = user.UpdateProfile("Tran Van Chu Tro", "0987 654 321", " ICTU ", "en", Later);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(user.UpdatedAtUtc, Is.EqualTo(CreatedAt));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    public void UpdateProfile_BlankFullName_Throws(string fullName)
    {
        var user = CreateLandlord();

        Assert.Throws<DomainValidationException>(() => user.UpdateProfile(fullName, null, null, null, Later));
    }

    [Test]
    public void UpdateProfile_FullNameOver100_Throws()
    {
        var user = CreateLandlord();

        Assert.Multiple(() =>
        {
            Assert.Throws<DomainValidationException>(
                () => user.UpdateProfile(new string('a', 101), null, null, null, Later));
            Assert.That(user.UpdateProfile(new string('a', 100), null, null, null, Later), Is.True);
        });
    }

    [TestCase("12345")]
    [TestCase("")]
    [TestCase("   ")]
    public void UpdateProfile_InvalidOrBlankPhone_Throws_KeepsNumber(string phone)
    {
        var user = CreateLandlord();

        Assert.Throws<DomainValidationException>(() => user.UpdateProfile(null, phone, null, null, Later));
        Assert.That(user.PhoneNumber, Is.EqualTo("0987654321"));
    }

    [Test]
    public void UpdateProfile_ValidPhoneWithSpaces_Normalized()
    {
        var user = CreateLandlord();

        user.UpdateProfile(null, "0912 345-678", null, null, Later);

        Assert.That(user.PhoneNumber, Is.EqualTo("0912345678"));
    }

    [TestCase("fr")]
    [TestCase("EN")]
    [TestCase("")]
    public void UpdateProfile_LanguageFrOrUppercaseEn_Throws(string language)
    {
        var user = CreateLandlord();

        Assert.Throws<DomainValidationException>(() => user.UpdateProfile(null, null, null, language, Later));
    }

    [Test]
    public void UpdateProfile_BlankCampus_Throws()
    {
        var user = CreateLandlord();

        Assert.Throws<DomainValidationException>(() => user.UpdateProfile(null, null, "  ", null, Later));
    }

    [Test]
    public void UpdateProfile_OneInvalidValue_ChangesNothing()
    {
        var user = CreateLandlord();

        Assert.Throws<DomainValidationException>(
            () => user.UpdateProfile("Valid Name", null, "TNUT", "fr", Later));

        Assert.Multiple(() =>
        {
            Assert.That(user.FullName, Is.EqualTo("Tran Van Chu Tro"));
            Assert.That(user.SelectedCampusId, Is.EqualTo("ICTU"));
            Assert.That(user.UpdatedAtUtc, Is.EqualTo(CreatedAt));
        });
    }

    [Test]
    public void UpdateProfile_NeverChangesRoleEmailOrVerification()
    {
        var user = CreateLandlord();

        user.UpdateProfile("Other", "0912345678", "TUMP", "vi", Later);

        Assert.Multiple(() =>
        {
            Assert.That(user.Role, Is.EqualTo(UserRole.Landlord));
            Assert.That(user.Email.Value, Is.EqualTo("landlord@example.com"));
            Assert.That(user.IsVerified, Is.False);
            Assert.That(user.PasswordHash, Is.EqualTo("hash"));
            Assert.That(user.Version, Is.EqualTo(1), "The repository bumps Version, not the aggregate.");
        });
    }
}
