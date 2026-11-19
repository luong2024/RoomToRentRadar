using System.Globalization;
using System.Linq;
using NUnit.Framework;
using RoomToRentRadar.Resources.Strings;
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

    [Test]
    public void PlaceholderCatalog_AllTitleKeys_ExistInBothResx()
    {
        var en = new CultureInfo("en-US");
        var vi = new CultureInfo("vi-VN");

        foreach (var entry in PlaceholderCatalog.All)
        {
            var enTitle = AppResources.GetString(entry.TitleKey, en);
            var viTitle = AppResources.GetString(entry.TitleKey, vi);

            // AppResources.GetString returns "[key]" when the key is missing from the resx.
            Assert.That(enTitle, Is.Not.EqualTo($"[{entry.TitleKey}]"), $"Missing EN title for {entry.Route}");
            Assert.That(viTitle, Is.Not.EqualTo($"[{entry.TitleKey}]"), $"Missing VI title for {entry.Route}");
            Assert.That(enTitle, Is.Not.Empty, $"Empty EN title for {entry.Route}");
            Assert.That(viTitle, Is.Not.Empty, $"Empty VI title for {entry.Route}");
        }

        Assert.That(AppResources.GetString("Placeholder_ComingIn", en), Is.Not.EqualTo("[Placeholder_ComingIn]"));
        Assert.That(AppResources.GetString("Placeholder_ComingIn", vi), Is.Not.EqualTo("[Placeholder_ComingIn]"));
    }

    [Test]
    public void ComingIn_FormatsTaskIdInBothCultures()
    {
        var en = new CultureInfo("en-US");
        var vi = new CultureInfo("vi-VN");

        var enText = string.Format(en, AppResources.GetString("Placeholder_ComingIn", en), "T02.1");
        var viText = string.Format(vi, AppResources.GetString("Placeholder_ComingIn", vi), "T02.1");

        Assert.That(enText, Is.EqualTo("Coming in T02.1"));
        Assert.That(viText, Is.EqualTo("Sẽ có trong T02.1"));
    }
}