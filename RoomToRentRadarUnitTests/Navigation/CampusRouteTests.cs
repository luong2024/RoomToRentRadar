using RoomToRentRadar.Services.Navigation.Core;

namespace RoomToRentRadarUnitTests.Navigation;

/// <summary>
/// T13.4 regression (found on the emulator): Shell treats a pushed route whose first segment is a tab
/// route as a relative path to that tab and throws "Relative routing to shell elements is currently not supported".
/// </summary>
[TestFixture]
public class CampusRouteTests
{
    private static readonly string[] ShellElementRoutes =
    [
        AppRoutes.Explore, AppRoutes.Map, AppRoutes.Saved, AppRoutes.MyListings,
        AppRoutes.Messages, AppRoutes.Moderation, AppRoutes.Account, AppRoutes.Login
    ];

    [Test]
    public void AccountCampusRoute_DoesNotStartWithAShellElementRoute()
    {
        var firstSegment = AppRoutes.AccountCampus.Split('/')[0];

        Assert.That(ShellElementRoutes, Does.Not.Contain(firstSegment));
    }
}
