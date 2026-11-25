using System.Globalization;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Pages.Controls.Cards.Models;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadarUnitTests.PageModels;

/// <summary>T01.2 — room card texts (US01 AC2, US15).</summary>
[TestFixture]
public class RoomCardDisplayModelTests
{
    [TearDown]
    public void TearDown() => UseLanguage("en-US");

    private static void UseLanguage(string culture) =>
        LocalizedStrings.Instance.SetCulture(new CultureInfo(culture));

    private static RoomCardDisplayModel Card(
        decimal price = 2_500_000,
        Furnishing? furnishing = Furnishing.Full,
        string thumbnail = "") => new()
    {
        Id = "r1",
        Title = "Private room with balcony",
        PricePerMonth = price,
        AreaSquareMeters = 25,
        DistanceMeters = 1200,
        RoomType = RoomType.BoardingRoom,
        Furnishing = furnishing,
        CampusName = "TNUT",
        ThumbnailUrl = thumbnail,
        IsVerified = true
    };

    [TestCase("en-US", 2_500_000, "2.5M/mo")]
    [TestCase("vi-VN", 2_500_000, "2,5 tr/tháng")]
    [TestCase("en-US", 1_000_000, "1M/mo")]
    [TestCase("en-US", 1_250_000, "1.25M/mo")]
    [TestCase("en-US", 950_000, "950k/mo")]
    [TestCase("vi-VN", 950_000, "950k/tháng")]
    [TestCase("en-US", 4_000_000, "4M/mo")]
    public void PriceText_UsesTheLanguageAndUnit(string culture, int price, string expected)
    {
        UseLanguage(culture);

        Assert.That(Card(price).PriceText, Is.EqualTo(expected));
    }

    [TestCase("en-US", "25 m² · Boarding room · Furnished")]
    [TestCase("vi-VN", "25 m² · Phòng trọ · Có nội thất")]
    public void MetaText_ListsAreaTypeAndFurnishing(string culture, string expected)
    {
        UseLanguage(culture);

        Assert.That(Card().MetaText, Is.EqualTo(expected));
    }

    [TestCase(Furnishing.Basic, "25 m² · Boarding room · Partly furnished")]
    [TestCase(Furnishing.Unfurnished, "25 m² · Boarding room · Unfurnished")]
    [TestCase(null, "25 m² · Boarding room")]
    public void MetaText_ReflectsFurnishing_AndOmitsUnknown(Furnishing? furnishing, string expected)
    {
        UseLanguage("en-US");

        Assert.That(Card(furnishing: furnishing).MetaText, Is.EqualTo(expected));
    }

    [TestCase("en-US", "1.2 km to TNUT")]
    [TestCase("vi-VN", "1,2 km tới TNUT")]
    public void DistanceText_NamesTheCampus(string culture, string expected)
    {
        UseLanguage(culture);

        Assert.That(Card().DistanceText, Is.EqualTo(expected));
    }

    [TestCase("http://10.0.2.2:5000/media/a.jpg", true)]
    [TestCase("https://cdn.example.com/a.jpg", true)]
    [TestCase("", false)]
    [TestCase("/media/a.jpg", false)]
    [TestCase("file:///a.jpg", false)]
    public void ThumbnailUri_OnlyForHttpAddresses(string url, bool hasUri)
    {
        Assert.That(Card(thumbnail: url).ThumbnailUri is not null, Is.EqualTo(hasUri));
    }

    [Test]
    public void AccessibilityText_ReadsTheWholeCard()
    {
        UseLanguage("en-US");

        Assert.That(
            Card().AccessibilityText,
            Is.EqualTo("Private room with balcony, 2.5M/mo, 25 m² · Boarding room · Furnished, 1.2 km to TNUT, Verified"));
    }

    [TestCase("en-US")]
    [TestCase("vi-VN")]
    public void ExploreAndCardKeys_ExistInBothLanguages(string culture)
    {
        string[] keys =
        [
            "Card_PriceMillion", "Card_PriceThousand", "Card_Distance", "Card_Furnishing_Full",
            "Card_Furnishing_Basic", "Card_Furnishing_Unfurnished", "Card_Verified", "Card_Compare",
            "Card_SaveDescription", "Explore_Near", "Explore_CampusChipDescription",
            "Explore_NotificationsDescription", "Explore_NotificationsUnreadDescription", "Explore_MapDescription",
            "Explore_SaveSearch", "Explore_CompareCount", "Explore_CompareNow", "Explore_LoadMoreError"
        ];
        var english = new CultureInfo("en-US");
        var target = new CultureInfo(culture);

        Assert.Multiple(() =>
        {
            foreach (var key in keys)
            {
                var value = AppResources.GetString(key, target);
                Assert.That(value, Is.Not.Empty.And.Not.StartsWith("["), key);
                if (culture != "en-US")
                {
                    Assert.That(value, Is.Not.EqualTo(AppResources.GetString(key, english)), $"{key} is not translated.");
                }
            }
        });
    }
}
