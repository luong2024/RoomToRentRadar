using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class ListingFilterFingerprintTests
{
    [Test]
    public void EmptyFilters_ProduceAStableFingerprint()
    {
        var first = ListingFilterFingerprint.Compute(new ListingFilters());
        var second = ListingFilterFingerprint.Compute(new ListingFilters());

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.Empty);
            Assert.That(first, Is.EqualTo(second));
        });
    }

    [Test]
    public void EqualFilters_ProduceTheSameFingerprint()
    {
        var filters = new ListingFilters(
            5_000_000,
            8_000_000,
            25.5,
            42d,
            2,
            RoomType.Studio,
            new CampusFilter(10.7626, 106.6602, 3.5));

        var copy = filters with { };

        Assert.That(
            ListingFilterFingerprint.Compute(filters),
            Is.EqualTo(ListingFilterFingerprint.Compute(copy)));
    }

    [Test]
    public void AnyChangedFilter_ChangesTheFingerprint()
    {
        var baseline = new ListingFilters(
            5_000_000,
            8_000_000,
            25.5,
            42d,
            2,
            RoomType.Studio,
            new CampusFilter(10.7626, 106.6602, 3.5));

        var variants = new[]
        {
            baseline with { MinPrice = 5_000_001 },
            baseline with { MaxPrice = 8_000_001 },
            baseline with { MinAreaM2 = 25.6 },
            baseline with { MaxAreaM2 = 42.1 },
            baseline with { Bedrooms = 3 },
            baseline with { RoomType = RoomType.House },
            baseline with { Campus = new CampusFilter(10.7627, 106.6602, 3.5) },
            baseline with { Campus = new CampusFilter(10.7626, 106.6603, 3.5) },
            baseline with { Campus = new CampusFilter(10.7626, 106.6602, 3.6) },
            baseline with { Campus = null }
        };

        var fingerprint = ListingFilterFingerprint.Compute(baseline);
        var fingerprints = variants.Select(ListingFilterFingerprint.Compute).ToList();

        Assert.That(fingerprints, Has.None.EqualTo(fingerprint));
    }
}
