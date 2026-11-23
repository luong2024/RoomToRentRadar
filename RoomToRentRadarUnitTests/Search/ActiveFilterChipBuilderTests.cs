using System.Globalization;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Resources.Strings;
using RoomToRentRadar.Services.Search.Core;

namespace RoomToRentRadarUnitTests.Search;

[TestFixture]
public class ActiveFilterChipBuilderTests
{
    private static readonly CultureInfo En = new("en-US");
    private static readonly CultureInfo Vi = new("vi-VN");

    [SetUp]
    public void SetUp() => LocalizedStrings.Instance.SetCulture(En);

    [TearDown]
    public void TearDown() => LocalizedStrings.Instance.SetCulture(En);

    [TestCase(3_000_000L, "Under 3M")]
    [TestCase(1_500_000L, "Under 1.5M")]
    [TestCase(950_000L, "Under 950k")]
    public void Build_MaxRentOnly_FormatsUnderAmountInEnglish(long maxRent, string expected)
    {
        var chips = ActiveFilterChipBuilder.Build(new RoomSearchCriteria { MaxRentVnd = maxRent });

        Assert.That(chips, Has.Count.EqualTo(1));
        Assert.That(chips[0].Kind, Is.EqualTo(ActiveFilterChipKind.Price));
        Assert.That(chips[0].Label, Is.EqualTo(expected));
        Assert.That(chips[0].RemoveAccessibilityLabel, Is.EqualTo($"Remove filter {expected}"));
    }

    [Test]
    public void Build_MinRentOnly_FormatsFromAmount()
    {
        var chips = ActiveFilterChipBuilder.Build(new RoomSearchCriteria { MinRentVnd = 1_500_000 });

        Assert.That(chips[0].Label, Is.EqualTo("From 1.5M"));
    }

    [Test]
    public void Build_RentRange_FormatsBothBounds()
    {
        var chips = ActiveFilterChipBuilder.Build(new RoomSearchCriteria
        {
            MinRentVnd = 1_500_000,
            MaxRentVnd = 3_000_000
        });

        Assert.That(chips[0].Label, Is.EqualTo("1.5M – 3M"));
    }

    [Test]
    public void Build_RentRangeWithMixedUnits_KeepsSuffixPerBound()
    {
        var criteria = new RoomSearchCriteria { MinRentVnd = 950_000, MaxRentVnd = 1_500_000 };

        LocalizedStrings.Instance.SetCulture(En);
        var en = ActiveFilterChipBuilder.Build(criteria);

        LocalizedStrings.Instance.SetCulture(Vi);
        var vi = ActiveFilterChipBuilder.Build(criteria);

        Assert.That(en[0].Label, Is.EqualTo("950k – 1.5M"));
        Assert.That(vi[0].Label, Is.EqualTo("950k – 1,5 tr"));
    }

    [Test]
    public void Build_AreaBounds_FormatsMinMaxAndRange()
    {
        var min = ActiveFilterChipBuilder.Build(new RoomSearchCriteria { MinAreaM2 = 15 });
        var max = ActiveFilterChipBuilder.Build(new RoomSearchCriteria { MaxAreaM2 = 40 });
        var range = ActiveFilterChipBuilder.Build(new RoomSearchCriteria { MinAreaM2 = 15, MaxAreaM2 = 40 });

        Assert.That(min[0].Label, Is.EqualTo("≥ 15 m²"));
        Assert.That(max[0].Label, Is.EqualTo("≤ 40 m²"));
        Assert.That(range[0].Label, Is.EqualTo("15–40 m²"));
        Assert.That(range[0].Kind, Is.EqualTo(ActiveFilterChipKind.Area));
    }

    [Test]
    public void Build_RoomTypes_ProducesOneChipPerTypeInEnumOrder()
    {
        var chips = ActiveFilterChipBuilder.Build(new RoomSearchCriteria
        {
            RoomTypes = new[] { RoomType.MiniApartment, RoomType.BoardingRoom }
        });

        Assert.That(chips.Select(c => c.Label), Is.EqualTo(new[] { "Boarding room", "Mini apartment" }));
        Assert.That(chips.Select(c => c.Kind), Is.All.EqualTo(ActiveFilterChipKind.RoomType));
        Assert.That(chips.Select(c => c.RoomType), Is.EqualTo(new RoomType?[] { RoomType.BoardingRoom, RoomType.MiniApartment }));
    }

    [TestCase(2.0, "≤ 2 km")]
    [TestCase(1.5, "≤ 1.5 km")]
    public void Build_MaxDistance_FormatsCompactLabel(double km, string expected)
    {
        var chips = ActiveFilterChipBuilder.Build(new RoomSearchCriteria { MaxDistanceKm = km });

        Assert.That(chips[0].Kind, Is.EqualTo(ActiveFilterChipKind.MaxDistance));
        Assert.That(chips[0].Label, Is.EqualTo(expected));
    }

    [Test]
    public void Build_AllFilters_OrderIsPriceAreaRoomTypesDistance()
    {
        var chips = ActiveFilterChipBuilder.Build(new RoomSearchCriteria
        {
            MaxRentVnd = 2_000_000,
            MinAreaM2 = 15,
            RoomTypes = new[] { RoomType.Dormitory },
            MaxDistanceKm = 3
        });

        Assert.That(chips.Select(c => c.Kind), Is.EqualTo(new[]
        {
            ActiveFilterChipKind.Price,
            ActiveFilterChipKind.Area,
            ActiveFilterChipKind.RoomType,
            ActiveFilterChipKind.MaxDistance
        }));
    }

    [Test]
    public void Build_VietnameseCulture_FormatsLabelsAndAccessibilityText()
    {
        LocalizedStrings.Instance.SetCulture(Vi);

        var chips = ActiveFilterChipBuilder.Build(new RoomSearchCriteria
        {
            MinRentVnd = 1_500_000,
            MaxRentVnd = 3_000_000,
            RoomTypes = new[] { RoomType.BoardingRoom },
            MaxDistanceKm = 2
        });

        Assert.That(chips[0].Label, Is.EqualTo("1,5 – 3 tr"));
        Assert.That(chips[1].Label, Is.EqualTo("Phòng trọ"));
        Assert.That(chips[2].Label, Is.EqualTo("≤ 2 km"));
        Assert.That(chips[0].RemoveAccessibilityLabel, Is.EqualTo("Gỡ bộ lọc 1,5 – 3 tr"));
    }

    [Test]
    public void Build_VietnameseUnderAmount_UsesCommaDecimalSeparator()
    {
        LocalizedStrings.Instance.SetCulture(Vi);

        var chips = ActiveFilterChipBuilder.Build(new RoomSearchCriteria { MaxRentVnd = 1_500_000 });

        Assert.That(chips[0].Label, Is.EqualTo("Dưới 1,5 tr"));
    }
}
