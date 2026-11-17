using RoomToRentRadar.Client.Adapters.Mocks.SampleData;
using RoomToRentRadar.Datastore.Sqlite.Seed;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadarUnitTests.ClientAdapters.Mocks;

/// <summary>
/// T00.14 Phase 8 (AC-4, PLAN Q6/Q11): the mock sample data must mirror the current demo seed exactly.
/// These tests break ON PURPOSE when T00.10 expands the seed — update SampleRooms/SampleCampuses then.
/// </summary>
[TestFixture]
public class MockSeedDriftTests
{
    [Test]
    public void SampleRooms_MirrorAvailableSeedRooms()
    {
        var seedRooms = RoomSeedData.CreateRooms().Where(r => r.Status == ListingStatus.Available).ToList();
        Assert.That(SampleRooms.All, Has.Count.EqualTo(seedRooms.Count));

        foreach (var seed in seedRooms)
        {
            var sample = SampleRooms.All.SingleOrDefault(r => r.Id == seed.Id);
            Assert.That(sample, Is.Not.Null, $"Seed room '{seed.Id}' is missing from SampleRooms.");
            Assert.Multiple(() =>
            {
                Assert.That(sample!.Title, Is.EqualTo(seed.Title), seed.Id);
                Assert.That(sample.MonthlyRentVnd, Is.EqualTo(seed.MonthlyRentVnd), seed.Id);
                Assert.That(sample.AreaM2, Is.EqualTo(seed.AreaM2), seed.Id);
                Assert.That(sample.RoomType, Is.EqualTo(seed.RoomType), seed.Id);
                Assert.That(sample.Latitude, Is.EqualTo(seed.Latitude), seed.Id);
                Assert.That(sample.Longitude, Is.EqualTo(seed.Longitude), seed.Id);
                Assert.That(sample.PublishedAtUtc, Is.EqualTo(seed.PublishedAtUtc), seed.Id);
                Assert.That(sample.Description, Is.EqualTo(seed.Description), seed.Id);
                Assert.That(sample.Ward, Is.EqualTo(seed.Ward), seed.Id);
                Assert.That(sample.District, Is.EqualTo(seed.District), seed.Id);
            });
        }
    }

    [Test]
    public void SampleRooms_ContainOnlyTheAvailableSeedRoomIds()
    {
        var seedIds = RoomSeedData.CreateRooms()
            .Where(r => r.Status == ListingStatus.Available)
            .Select(r => r.Id)
            .ToList();

        Assert.That(SampleRooms.All.Select(r => r.Id).ToList(), Is.EquivalentTo(seedIds));
    }

    [Test]
    public void SampleCampuses_MirrorCampusSeedData()
    {
        var seed = CampusSeedData.CreateCampuses();
        Assert.That(SampleCampuses.All, Has.Count.EqualTo(seed.Count));

        for (var i = 0; i < seed.Count; i++)
        {
            var sample = SampleCampuses.All[i];
            Assert.Multiple(() =>
            {
                Assert.That(sample.Id, Is.EqualTo(seed[i].Id));
                Assert.That(sample.Name, Is.EqualTo(seed[i].Name));
                Assert.That(sample.ShortName, Is.EqualTo(seed[i].ShortName));
                Assert.That(sample.Latitude, Is.EqualTo(seed[i].Latitude));
                Assert.That(sample.Longitude, Is.EqualTo(seed[i].Longitude));
                Assert.That(sample.IsDefault, Is.EqualTo(seed[i].IsDefault));
            });
        }
    }
}