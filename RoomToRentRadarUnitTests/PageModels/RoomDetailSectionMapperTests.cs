using System.Globalization;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Pages.Controls.RoomDetail.Models;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadarUnitTests.PageModels;

[TestFixture]
public class RoomDetailSectionMapperTests
{
    [SetUp]
    public void SetUp()
    {
        LocalizedStrings.Instance.SetCulture(new CultureInfo("en-US"));
    }

    [TearDown]
    public void TearDown()
    {
        LocalizedStrings.Instance.SetCulture(new CultureInfo("en-US"));
    }

    [Test]
    public void ExtractInitials_HandlesEmptySingleAndMultiWordNames()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RoomDetailSectionMapper.ExtractInitials(""), Is.EqualTo("LL"));
            Assert.That(RoomDetailSectionMapper.ExtractInitials("An"), Is.EqualTo("AN"));
            Assert.That(RoomDetailSectionMapper.ExtractInitials("A"), Is.EqualTo("A"));
            Assert.That(RoomDetailSectionMapper.ExtractInitials("Nguyen Van Minh"), Is.EqualTo("NM"));
        });
    }

    [TestCase(0, true, "0/5 · Verified")]
    [TestCase(3, false, "3/5 · Unverified")]
    [TestCase(80, true, "4/5 · Verified")]
    public void BuildSafetyChecklist_NormalizesScoreAndMapsAllFiveCriteria(
        int rawScore,
        bool isVerified,
        string expectedScoreText)
    {
        var safety = new SafetyDto(
            Score: rawScore,
            IsVerified: isVerified,
            Features: [SafetyFeature.Cctv, SafetyFeature.FireSafety]);

        var (scoreFormatted, items) = RoomDetailSectionMapper.BuildSafetyChecklist(safety);

        Assert.Multiple(() =>
        {
            Assert.That(scoreFormatted, Is.EqualTo(expectedScoreText));
            Assert.That(items, Has.Count.EqualTo(5));
            Assert.That(items.Single(i => i.Feature == SafetyFeature.Cctv).IsVerified, Is.True);
            Assert.That(items.Single(i => i.Feature == SafetyFeature.FireSafety).IsVerified, Is.True);
            Assert.That(items.Single(i => i.Feature == SafetyFeature.SecureEntrance).IsVerified, Is.False);
        });
    }

    [Test]
    public void BuildAmenities_AndGroupAmenityRows_PairsItemsIntoTwoColumns()
    {
        var amenities = new[] { Amenity.Wifi, Amenity.AirConditioner, Amenity.Desk };

        var displayItems = RoomDetailSectionMapper.BuildAmenities(amenities);
        var rows = RoomDetailSectionMapper.GroupAmenityRows(displayItems);

        Assert.Multiple(() =>
        {
            Assert.That(displayItems, Has.Count.EqualTo(3));
            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows[0].HasRight, Is.True);
            Assert.That(rows[1].HasRight, Is.False);
        });
    }

    [Test]
    public void BuildHouseRules_WhenNull_ReturnsEmptyList()
    {
        var rules = RoomDetailSectionMapper.BuildHouseRules(null);
        Assert.That(rules, Is.Empty);
    }

    [Test]
    public void BuildCommuteItems_MapsEstimateAndExactDurations()
    {
        var items = new TravelTimeItemDto[]
        {
            new(TravelMode.Motorbike, 1800, 300, IsEstimate: false),
            new(TravelMode.Bus, 1800, 1080, IsEstimate: true)
        };

        var mapped = RoomDetailSectionMapper.BuildCommuteItems(items);

        Assert.Multiple(() =>
        {
            Assert.That(mapped, Has.Count.EqualTo(2));
            Assert.That(mapped[0].Mode, Is.EqualTo(TravelMode.Motorbike));
            Assert.That(mapped[0].TimeFormatted, Does.Contain("5"));
            Assert.That(mapped[1].Mode, Is.EqualTo(TravelMode.Bus));
            Assert.That(mapped[1].TimeFormatted, Does.Contain("18"));
        });
    }
}
