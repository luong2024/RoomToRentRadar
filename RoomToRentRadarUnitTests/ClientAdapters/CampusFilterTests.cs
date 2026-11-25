using RoomToRentRadar.Client.Adapters.Mocks.SampleData;
using RoomToRentRadar.Client.Adapters.Utilities;
using RoomToRentRadar.Ports.Contracts.Responses.Campuses;

namespace RoomToRentRadarUnitTests.ClientAdapters;

/// <summary>T13.4 AC-4 — campus search on SCR-ACC-02.</summary>
[TestFixture]
public class CampusFilterTests
{
    private static readonly IReadOnlyList<CampusDto> All = SampleCampuses.All;

    private static string[] Ids(string? query) => CampusFilter.Filter(All, query).Select(c => c.Id).ToArray();

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void EmptyOrWhitespaceQuery_ReturnsAll(string? query)
    {
        Assert.That(Ids(query), Is.EqualTo(All.Select(c => c.Id)));
    }

    [TestCase("tnut")]
    [TestCase("  TnUt ")]
    public void CodeMatch_CaseInsensitive(string query)
    {
        Assert.That(Ids(query), Is.EqualTo(new[] { "TNUT" }));
    }

    [TestCase("kinh te", "TUEBA")]
    [TestCase("nong lam", "TUAF")]
    [TestCase("Y DƯỢC", "TUMP")]
    [TestCase("su pham", "TNUE")]
    public void NameMatch_WithoutDiacritics(string query, string expected)
    {
        Assert.That(Ids(query), Is.EqualTo(new[] { expected }));
    }

    [Test]
    public void DStroke_MatchesPlainD()
    {
        var expected = All.Where(c => c.Name.Contains("Đại học", StringComparison.Ordinal)).Select(c => c.Id).ToArray();

        Assert.That(expected, Is.Not.Empty);
        Assert.That(Ids("dai hoc"), Is.SupersetOf(expected));
    }

    [Test]
    public void DecomposedInput_StillMatches()
    {
        var decomposed = "Kinh tế".Normalize(System.Text.NormalizationForm.FormD);

        Assert.That(Ids(decomposed), Is.EqualTo(new[] { "TUEBA" }));
    }

    [Test]
    public void NoMatch_ReturnsEmpty()
    {
        Assert.That(Ids("xyz"), Is.Empty);
    }

    [Test]
    public void PreservesOriginalOrder()
    {
        var result = Ids("tn");

        Assert.That(result, Is.EqualTo(All.Select(c => c.Id).Where(result.Contains)));
        Assert.That(result.Length, Is.GreaterThan(1));
    }
}
