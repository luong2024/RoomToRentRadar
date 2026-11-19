using RoomToRentRadar.Client.Adapters.Mocks;

namespace RoomToRentRadarUnitTests.ClientAdapters.Mocks;

/// <summary>T00.14 Phase 8 (AC-4): MockCampusGateway serves the eight seed campuses, ICTU default first, deterministically.</summary>
[TestFixture]
public class MockCampusGatewayTests
{
    private readonly MockCampusGateway _gateway = new();

    [Test]
    public async Task ListCampusesAsync_ReturnsEightCampuses_IctuDefaultFirst()
    {
        var campuses = await _gateway.ListCampusesAsync();

        Assert.That(campuses, Has.Count.EqualTo(8));
        Assert.That(campuses[0].Id, Is.EqualTo("ICTU"));
        Assert.That(campuses[0].IsDefault, Is.True);
    }

    [Test]
    public async Task ListCampusesAsync_ExactlyOneDefaultCampus()
    {
        var campuses = await _gateway.ListCampusesAsync();

        Assert.That(campuses.Count(c => c.IsDefault), Is.EqualTo(1));
    }

    [Test]
    public async Task ListCampusesAsync_IdsMatchCampusReferenceData()
    {
        var campuses = await _gateway.ListCampusesAsync();

        Assert.That(campuses.Select(c => c.Id).ToList(), Is.EqualTo(new[]
        {
            "ICTU", "TNUS", "SFL", "TUEBA", "TUAF", "TNUE", "TUMP", "TNUT"
        }));
    }

    [Test]
    public async Task ListCampusesAsync_TwoCalls_ReturnIdenticalData()
    {
        var first = await _gateway.ListCampusesAsync();
        var second = await _gateway.ListCampusesAsync();

        Assert.That(second, Is.EqualTo(first));
    }
}