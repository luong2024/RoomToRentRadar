using NUnit.Framework;
using RoomToRentRadar.Client.Adapters.Utilities;

namespace RoomToRentRadarUnitTests.ClientAdapters;

[TestFixture]
public class UserProfileFormatterTests
{
    [TestCase(null, "ST")]
    [TestCase("", "ST")]
    [TestCase("   ", "ST")]
    public void ComputeInitials_NullOrWhitespace_ReturnsDefaultST(string? input, string expected)
    {
        var result = UserProfileFormatter.ComputeInitials(input);
        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase("Lê Minh", "LM")]
    [TestCase("Nguyễn Văn Tuân", "NT")]
    [TestCase("Trần Thùy Duyên", "TD")]
    public void ComputeInitials_FullNameWithSpaces_ReturnsFirstAndLastInitial(string input, string expected)
    {
        var result = UserProfileFormatter.ComputeInitials(input);
        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase("Minh", "MI")]
    [TestCase("A", "A")]
    public void ComputeInitials_SingleWordName_ReturnsInitialsCorrectly(string input, string expected)
    {
        var result = UserProfileFormatter.ComputeInitials(input);
        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase("minh.le@ictu.edu.vn", "ML")]
    [TestCase("john_doe@example.com", "JD")]
    [TestCase("admin@ictu.edu.vn", "AD")]
    public void ComputeInitials_EmailFormat_ExtractsUsernameInitials(string input, string expected)
    {
        var result = UserProfileFormatter.ComputeInitials(input);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void FormatRoleBadge_LandlordVerified_ReturnsVerifiedBadge()
    {
        var badge = UserProfileFormatter.FormatRoleBadge("Landlord", isVerified: true);
        Assert.That(badge, Is.EqualTo("Landlord · verified"));
    }

    [Test]
    public void FormatRoleBadge_LandlordUnverified_ReturnsPendingVerificationBadge()
    {
        var badge = UserProfileFormatter.FormatRoleBadge("Landlord", isVerified: false);
        Assert.That(badge, Is.EqualTo("Landlord · pending verification"));
    }

    [Test]
    public void FormatRoleBadge_StudentIctuSso_ReturnsIctuSsoBadge()
    {
        var badge = UserProfileFormatter.FormatRoleBadge("Student", isVerified: true, authProvider: "IctuSso");
        Assert.That(badge, Is.EqualTo("Student · signed in with ICTU"));
    }

    [Test]
    public void FormatRoleBadge_StudentEmailPassword_ReturnsVerifiedEmailBadge()
    {
        var badge = UserProfileFormatter.FormatRoleBadge("Student", isVerified: true, authProvider: "EmailPassword");
        Assert.That(badge, Is.EqualTo("Student · verified email"));
    }

    [Test]
    public void FormatRoleBadge_Admin_ReturnsAdminBadge()
    {
        var badge = UserProfileFormatter.FormatRoleBadge("Admin", isVerified: true);
        Assert.That(badge, Is.EqualTo("Administrator · ICTU Radar"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void FormatRoleBadge_NullOrWhitespace_ReturnsDefaultStudentBadge(string? role)
    {
        var badge = UserProfileFormatter.FormatRoleBadge(role, isVerified: false);
        Assert.That(badge, Is.EqualTo("Student · signed in with ICTU"));
    }

    [Test]
    public void FormatRoleBadge_Vietnamese_LandlordVerified_ReturnsVietnameseVerifiedBadge()
    {
        var badge = UserProfileFormatter.FormatRoleBadge("Landlord", isVerified: true, language: "vi-VN");
        Assert.That(badge, Is.EqualTo("Chủ trọ · đã xác minh"));
    }

    [Test]
    public void FormatRoleBadge_Vietnamese_LandlordUnverified_ReturnsVietnamesePendingBadge()
    {
        var badge = UserProfileFormatter.FormatRoleBadge("Landlord", isVerified: false, language: "vi-VN");
        Assert.That(badge, Is.EqualTo("Chủ trọ · chờ xác minh"));
    }

    [Test]
    public void FormatRoleBadge_Vietnamese_StudentIctuSso_ReturnsVietnameseIctuBadge()
    {
        var badge = UserProfileFormatter.FormatRoleBadge("Student", isVerified: true, authProvider: "IctuSso", language: "vi");
        Assert.That(badge, Is.EqualTo("Sinh viên · đăng nhập qua ICTU"));
    }

    [Test]
    public void FormatRoleBadge_Vietnamese_Admin_ReturnsVietnameseAdminBadge()
    {
        var badge = UserProfileFormatter.FormatRoleBadge("Admin", isVerified: true, language: "vi");
        Assert.That(badge, Is.EqualTo("Quản trị viên · ICTU Radar"));
    }
}
