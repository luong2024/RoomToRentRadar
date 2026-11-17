using System.Globalization;
using RoomToRentRadar.Core.Localization;

namespace RoomToRentRadarUnitTests.Features.Localization;

[TestFixture]
public class LocalizationResourceManagerTests
{
    private CultureInfo _initialCulture = null!;

    [SetUp]
    public void SetUp()
    {
        _initialCulture = LocalizationResourceManager.Instance.CurrentCulture;
    }

    [TearDown]
    public void TearDown()
    {
        LocalizationResourceManager.Instance.SetCulture(_initialCulture);
    }

    [Test]
    public void Instance_WhenInitialized_ReturnsNonNullSingleton()
    {
        var manager = LocalizationResourceManager.Instance;
        Assert.That(manager, Is.Not.Null);
    }

    [Test]
    public void SetCulture_ToVietnamese_SetsIsVietnameseTrueAndReturnsVietnameseStrings()
    {
        var manager = LocalizationResourceManager.Instance;
        manager.SetCulture(new CultureInfo("vi"));

        Assert.Multiple(() =>
        {
            Assert.That(manager.IsVietnamese, Is.True);
            Assert.That(manager.IsEnglish, Is.False);
            Assert.That(manager.CurrentLanguageCode, Is.EqualTo("vi"));
            Assert.That(manager["App_Name"], Is.EqualTo("Smart Rent"));
            Assert.That(manager["Title_ExploreRooms"], Is.EqualTo("Khám phá phòng trọ"));
            Assert.That(manager["Action_Retry"], Is.EqualTo("Thử lại"));
            Assert.That(manager["Nav_Home"], Is.EqualTo("Trang chủ"));
        });
    }

    [Test]
    public void SetCulture_ToEnglish_SetsIsEnglishTrueAndReturnsEnglishStrings()
    {
        var manager = LocalizationResourceManager.Instance;
        manager.SetCulture(new CultureInfo("en"));

        Assert.Multiple(() =>
        {
            Assert.That(manager.IsVietnamese, Is.False);
            Assert.That(manager.IsEnglish, Is.True);
            Assert.That(manager.CurrentLanguageCode, Is.EqualTo("en"));
            Assert.That(manager["App_Name"], Is.EqualTo("Smart Rent"));
            Assert.That(manager["Title_ExploreRooms"], Is.EqualTo("Explore Rooms"));
            Assert.That(manager["Action_Retry"], Is.EqualTo("Retry"));
            Assert.That(manager["Nav_Home"], Is.EqualTo("Home"));
        });
    }

    [Test]
    public void ToggleLanguage_WhenCalled_SwitchesBetweenVietnameseAndEnglish()
    {
        var manager = LocalizationResourceManager.Instance;
        manager.SetCulture(new CultureInfo("vi"));

        manager.ToggleLanguage();
        Assert.That(manager.IsEnglish, Is.True);

        manager.ToggleLanguage();
        Assert.That(manager.IsVietnamese, Is.True);
    }

    [Test]
    public void Indexer_WhenGivenNonExistentKey_ReturnsKeyItselfWithoutThrowing()
    {
        var manager = LocalizationResourceManager.Instance;
        var result = manager["NonExistent_Key_XYZ"];

        Assert.That(result, Is.EqualTo("NonExistent_Key_XYZ"));
    }

    [Test]
    public void Indexer_WhenGivenNullOrWhitespace_ReturnsEmptyString()
    {
        var manager = LocalizationResourceManager.Instance;
        Assert.Multiple(() =>
        {
            Assert.That(manager[""], Is.EqualTo(string.Empty));
            Assert.That(manager["   "], Is.EqualTo(string.Empty));
        });
    }
}
