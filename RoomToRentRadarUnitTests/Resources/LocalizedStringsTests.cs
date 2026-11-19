using System.Globalization;
using NUnit.Framework;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadarUnitTests.Resources;

[TestFixture]
public class LocalizedStringsTests
{
    [TearDown]
    public void TearDown()
    {
        // Reset to English after each test
        LocalizedStrings.Instance.SetCulture(new CultureInfo("en-US"));
    }

    [Test]
    public void Indexer_DefaultEnglishCulture_ReturnsEnglishText()
    {
        LocalizedStrings.Instance.SetCulture(new CultureInfo("en-US"));

        var explore = LocalizedStrings.Instance["Nav_Explore"];
        var signOut = LocalizedStrings.Instance["Account_Item_SignOut"];
        var accountTitle = LocalizedStrings.Instance["Account_Title"];
        var savedTitle = LocalizedStrings.Instance["Saved_Title"];

        Assert.That(explore, Is.EqualTo("Explore"));
        Assert.That(signOut, Is.EqualTo("Sign out"));
        Assert.That(accountTitle, Is.EqualTo("Account"));
        Assert.That(savedTitle, Is.EqualTo("Saved Rooms"));
        Assert.That(LocalizedStrings.Instance["Common_Ok"], Is.EqualTo("OK"));
        Assert.That(LocalizedStrings.Instance["Account_Alert_CampusTitle"], Is.EqualTo("My Campus"));
    }

    [Test]
    public void Indexer_VietnameseCulture_ReturnsVietnameseText()
    {
        LocalizedStrings.Instance.SetCulture(new CultureInfo("vi-VN"));

        var explore = LocalizedStrings.Instance["Nav_Explore"];
        var signOut = LocalizedStrings.Instance["Account_Item_SignOut"];
        var accountTitle = LocalizedStrings.Instance["Account_Title"];
        var welcome = LocalizedStrings.Instance["Account_Guest_WelcomeTitle"];
        var savedTitle = LocalizedStrings.Instance["Saved_Title"];

        Assert.That(explore, Is.EqualTo("Khám phá"));
        Assert.That(signOut, Is.EqualTo("Đăng xuất"));
        Assert.That(accountTitle, Is.EqualTo("Tài khoản"));
        Assert.That(welcome, Is.EqualTo("Chào mừng đến với RoomToRentRadar"));
        Assert.That(savedTitle, Is.EqualTo("Phòng đã lưu"));
        Assert.That(LocalizedStrings.Instance["Common_Ok"], Is.EqualTo("Đồng ý"));
        Assert.That(LocalizedStrings.Instance["Account_Alert_CampusTitle"], Is.EqualTo("Trường của tôi"));
    }

    [Test]
    public void SetCulture_WhenInvoked_FiresPropertyChangedEvent()
    {
        bool propertyChangedFired = false;
        string? changedPropertyName = "initial";

        void Handler(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            propertyChangedFired = true;
            changedPropertyName = e.PropertyName;
        }

        LocalizedStrings.Instance.PropertyChanged += Handler;
        try
        {
            LocalizedStrings.Instance.SetCulture(new CultureInfo("vi-VN"));

            Assert.That(propertyChangedFired, Is.True);
            // null or empty signals that all properties/indexers changed
            Assert.That(string.IsNullOrEmpty(changedPropertyName), Is.True);
        }
        finally
        {
            LocalizedStrings.Instance.PropertyChanged -= Handler;
        }
    }

    [Test]
    public void Indexer_MissingKey_ReturnsKeyInBrackets()
    {
        var text = LocalizedStrings.Instance["Unknown_Key_12345"];
        Assert.That(text, Is.EqualTo("[Unknown_Key_12345]"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Indexer_NullOrWhitespace_ReturnsEmptyString(string? key)
    {
        var text = LocalizedStrings.Instance[key!];
        Assert.That(text, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Indexer_RoomDetail_EnglishAndVietnamese_ReturnsCorrectTranslations()
    {
        LocalizedStrings.Instance.SetCulture(new CultureInfo("en-US"));
        Assert.That(LocalizedStrings.Instance["RoomDetail_Title"], Is.EqualTo("Room Details"));
        Assert.That(LocalizedStrings.Instance["RoomDetail_VerifiedAvailable"], Is.EqualTo("Verified · Available"));
        Assert.That(LocalizedStrings.Instance["RoomDetail_MonthlyCosts"], Is.EqualTo("Monthly costs"));
        Assert.That(LocalizedStrings.Instance["RoomDetail_ContactDisabled"], Is.EqualTo("Contact is disabled for unavailable rooms"));
        Assert.That(LocalizedStrings.Instance["RoomDetail_SimilarRoomsNearby"], Is.EqualTo("Similar rooms nearby"));

        LocalizedStrings.Instance.SetCulture(new CultureInfo("vi-VN"));
        Assert.That(LocalizedStrings.Instance["RoomDetail_Title"], Is.EqualTo("Chi tiết phòng"));
        Assert.That(LocalizedStrings.Instance["RoomDetail_VerifiedAvailable"], Is.EqualTo("Đã xác minh · Còn phòng"));
        Assert.That(LocalizedStrings.Instance["RoomDetail_MonthlyCosts"], Is.EqualTo("Chi phí hàng tháng"));
        Assert.That(LocalizedStrings.Instance["RoomDetail_ContactDisabled"], Is.EqualTo("Tính năng liên hệ bị tắt đối với phòng đã hết"));
        Assert.That(LocalizedStrings.Instance["RoomDetail_SimilarRoomsNearby"], Is.EqualTo("Phòng tương tự gần đây"));
    }

    [Test]
    public void Resx_AllEnglishKeys_HaveCorrespondingVietnameseTranslation()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? enResx = null;
        string? viResx = null;
        while (dir != null)
        {
            var enCandidate = Path.Combine(dir.FullName, "RoomToRentRadar", "Resources", "Strings", "AppResources.resx");
            var viCandidate = Path.Combine(dir.FullName, "RoomToRentRadar", "Resources", "Strings", "AppResources.vi.resx");
            if (File.Exists(enCandidate) && File.Exists(viCandidate))
            {
                enResx = enCandidate;
                viResx = viCandidate;
                break;
            }
            dir = dir.Parent;
        }

        Assert.That(enResx, Is.Not.Null, "AppResources.resx path was not found.");
        Assert.That(viResx, Is.Not.Null, "AppResources.vi.resx path was not found.");

        var enKeys = System.Xml.Linq.XDocument.Load(enResx!)
            .Descendants("data")
            .Select(d => d.Attribute("name")?.Value)
            .Where(k => !string.IsNullOrEmpty(k))
            .ToHashSet();

        var viKeys = System.Xml.Linq.XDocument.Load(viResx!)
            .Descendants("data")
            .Select(d => d.Attribute("name")?.Value)
            .Where(k => !string.IsNullOrEmpty(k))
            .ToHashSet();

        Assert.That(enKeys, Is.EquivalentTo(viKeys), "English and Vietnamese resource keys must match 1:1.");
    }
}
