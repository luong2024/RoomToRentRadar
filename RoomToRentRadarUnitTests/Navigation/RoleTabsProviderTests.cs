using System.Linq;
using NUnit.Framework;
using RoomToRentRadar.Services.Navigation.Core;

namespace RoomToRentRadarUnitTests.Navigation;

[TestFixture]
public class RoleTabsProviderTests
{
    private static string[] Routes(string? role) =>
        RoleTabsProvider.GetTabs(role).Select(t => t.Route).ToArray();

    [Test]
    public void GetTabs_Guest_ReturnsExploreMapSavedMessagesAccount()
    {
        Assert.That(Routes(null), Is.EqualTo(new[]
        {
            AppRoutes.Explore, AppRoutes.Map, AppRoutes.Saved, AppRoutes.Messages, AppRoutes.Account
        }));
    }

    [Test]
    public void GetTabs_Student_SameAsGuest()
    {
        Assert.That(Routes("Student"), Is.EqualTo(Routes(null)));
    }

    [Test]
    public void GetTabs_Landlord_ReturnsExploreMyListingsMessagesAccount_NoMapNoSaved()
    {
        var routes = Routes("Landlord");

        Assert.That(routes, Is.EqualTo(new[]
        {
            AppRoutes.Explore, AppRoutes.MyListings, AppRoutes.Messages, AppRoutes.Account
        }));
        Assert.That(routes, Does.Not.Contain(AppRoutes.Map));
        Assert.That(routes, Does.Not.Contain(AppRoutes.Saved));
    }

    [Test]
    public void GetTabs_Admin_ReturnsExploreMapModerationMessagesAccount_NoSaved()
    {
        var routes = Routes("Admin");

        Assert.That(routes, Is.EqualTo(new[]
        {
            AppRoutes.Explore, AppRoutes.Map, AppRoutes.Moderation, AppRoutes.Messages, AppRoutes.Account
        }));
        Assert.That(routes, Does.Not.Contain(AppRoutes.Saved));
    }

    [Test]
    public void GetTabs_UnknownRole_FallsBackToStudent()
    {
        Assert.That(Routes("Superuser"), Is.EqualTo(Routes("Student")));
    }

    [Test]
    public void GetTabs_AnyRole_AtMostFiveTabsAndUniqueRoutes()
    {
        foreach (var role in new string?[] { null, "", "Student", "Landlord", "Admin", "Unknown" })
        {
            var tabs = RoleTabsProvider.GetTabs(role);

            Assert.That(tabs.Count, Is.LessThanOrEqualTo(5));
            Assert.That(tabs.Select(t => t.Route).Distinct().Count(), Is.EqualTo(tabs.Count));
        }
    }
}