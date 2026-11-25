using System.Globalization;
using RoomToRentRadar.Converters;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadarUnitTests.Converters;

/// <summary>T00.18 — short prices shared by the room card, room detail and compare card (ui-ux-spec §6).</summary>
[TestFixture]
public class CompactPriceFormattingTests
{
    [TearDown]
    public void TearDown() => LocalizedStrings.Instance.SetCulture(new CultureInfo("en-US"));

    private static CultureInfo Use(string name)
    {
        var culture = new CultureInfo(name);
        LocalizedStrings.Instance.SetCulture(culture);
        return culture;
    }

    [TestCase("en-US", 2_500_000, "2.5M")]
    [TestCase("en-US", 2_454_000, "2.454M")]
    [TestCase("en-US", 2_375_000, "2.375M")]
    [TestCase("en-US", 50_000_000, "50M")]
    [TestCase("en-US", 950_000, "950k")]
    [TestCase("en-US", 1_000, "1k")]
    [TestCase("vi-VN", 2_500_000, "2,5 tr")]
    [TestCase("vi-VN", 950_000, "950k")]
    public void CompactMoney_ShortensWholeThousands(string culture, long amount, string expected)
    {
        Assert.That(CultureFormatting.FormatCompactMoney(amount, Use(culture)), Is.EqualTo(expected));
    }

    // The short form must never change the amount: anything it cannot show exactly is written in full.
    [TestCase("en-US", 2_454_321, "2,454,321 ₫")]
    [TestCase("en-US", 2_454_500, "2,454,500 ₫")]
    [TestCase("en-US", 950_500, "950,500 ₫")]
    [TestCase("en-US", 999, "999 ₫")]
    [TestCase("en-US", 0, "0 ₫")]
    [TestCase("vi-VN", 2_454_321, "2.454.321 ₫")]
    public void CompactMoney_KeepsOddAmountsInFull(string culture, long amount, string expected)
    {
        Assert.That(CultureFormatting.FormatCompactMoney(amount, Use(culture)), Is.EqualTo(expected));
    }

    [Test]
    public void CompactMoney_NegativeAmount_ShowsTheFallback()
    {
        Assert.That(
            CultureFormatting.FormatCompactMoney(-1, Use("en-US")), Is.EqualTo(CultureFormatting.FallbackValue));
    }

    [TestCase("en-US", 2_500_000, "2.5M/mo")]
    [TestCase("vi-VN", 2_500_000, "2,5 tr/tháng")]
    [TestCase("en-US", 2_454_321, "2,454,321 ₫/mo")]
    [TestCase("vi-VN", 2_454_321, "2.454.321 ₫/tháng")]
    public void MonthlyPrice_AddsThePeriod(string culture, long amount, string expected)
    {
        Assert.That(CultureFormatting.FormatMonthlyPrice(amount, Use(culture)), Is.EqualTo(expected));
    }

    [TestCase("en-US", 2_400_000, "2.4M VND")]
    [TestCase("en-US", 950_000, "950k VND")]
    [TestCase("en-US", 2_454_321, "2,454,321 VND")]
    [TestCase("en-US", 0, "0 VND")]
    [TestCase("vi-VN", 2_400_000, "2,4 tr")]
    [TestCase("vi-VN", 2_454_321, "2.454.321 ₫")]
    public void DepositMoney_NamesTheCurrencyInEnglish(string culture, long amount, string expected)
    {
        Assert.That(CultureFormatting.FormatDepositMoney(amount, Use(culture)), Is.EqualTo(expected));
    }
}
