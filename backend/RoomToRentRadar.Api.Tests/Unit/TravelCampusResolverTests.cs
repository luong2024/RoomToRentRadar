using Microsoft.AspNetCore.Http;
using RoomToRentRadar.Api.Features.Travel;
using RoomToRentRadar.Shared.Campuses;

namespace RoomToRentRadar.Api.Tests.Unit;

[TestFixture]
public sealed class TravelCampusResolverTests
{
    [TestCase("bach-khoa")]
    [TestCase("khtn")]
    [TestCase("BACH-KHOA")]
    [TestCase("  khtn  ")]
    public void TryResolve_KnownCampus_ReturnsCatalogEntry(string campusId)
    {
        var resolved = TravelCampusResolver.TryResolve(campusId, out var campus, out var problem);
        var expected = CampusCatalog.FindById(campusId)!;

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.True);
            Assert.That(problem, Is.Null);
            Assert.That(campus.Id, Is.EqualTo(expected.Id));
            Assert.That(campus.Name, Is.EqualTo(expected.Name));
            Assert.That(campus.Lat, Is.EqualTo(expected.Lat));
            Assert.That(campus.Lng, Is.EqualTo(expected.Lng));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("hcmus")]
    [TestCase("bach khoa")]
    public void TryResolve_MissingOrUnknownCampus_ReturnsInvalidDestination(string? campusId)
    {
        var resolved = TravelCampusResolver.TryResolve(campusId, out _, out var problem);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.False);
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(
                problem.Extensions["code"],
                Is.EqualTo(TravelProblemFactory.InvalidDestination));
        });
    }

    [Test]
    public void CampusCatalog_IdsAndNames_AreUnique()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                CampusCatalog.All.Select(campus => campus.Id)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                Is.EqualTo(CampusCatalog.All.Count));
            Assert.That(
                CampusCatalog.All.Select(campus => campus.Name).Distinct().Count(),
                Is.EqualTo(CampusCatalog.All.Count));
        });
    }
}
