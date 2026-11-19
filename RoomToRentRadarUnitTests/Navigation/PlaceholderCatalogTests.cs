using System.Linq;
using NUnit.Framework;
using RoomToRentRadar.Services.Navigation.Core;

namespace RoomToRentRadarUnitTests.Navigation;

[TestFixture]
public class PlaceholderCatalogTests
{
    [Test]
    public void PlaceholderCatalog_AllTwelveRoutesAndTwoTabsHaveEntry()
    {
        Assert.That(AppRoutes.DetailRoutes.Count, Is.EqualTo(12));

        var routes = AppRoutes.DetailRoutes.Concat(new[] { AppRoutes.MyListings, AppRoutes.Moderation });
        foreach (var route in routes)
        {
            var entry = PlaceholderCatalog.Find(route);

            Assert.That(entry, Is.Not.Null, $"Missing catalog entry for {route}");
            Assert.That(entry!.Route, Is.EqualTo(route));
            Assert.That(entry.ScrId, Is.Not.Empty);
            Assert.That(entry.TaskId, Is.Not.Empty);
            Assert.That(entry.TitleKey, Is.Not.Empty);
        }

        Assert.That(PlaceholderCatalog.All.Count, Is.EqualTo(14));
    }

    [Test]
    public void PlaceholderCatalog_UnknownRoute_ReturnsNull()
    {
        Assert.That(PlaceholderCatalog.Find("does/not/exist"), Is.Null);
    }
}