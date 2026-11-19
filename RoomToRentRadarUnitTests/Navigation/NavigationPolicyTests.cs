using NUnit.Framework;
using RoomToRentRadar.Services.Navigation.Core;

namespace RoomToRentRadarUnitTests.Navigation;

[TestFixture]
public class NavigationPolicyTests
{
    [Test]
    public void RequiresSignIn_SavedAndMessages_TrueForGuestOnly()
    {
        Assert.That(NavigationPolicy.RequiresSignIn(AppRoutes.Saved), Is.True);
        Assert.That(NavigationPolicy.RequiresSignIn(AppRoutes.Messages), Is.True);
        Assert.That(NavigationPolicy.RequiresSignIn(AppRoutes.Explore), Is.False);
        Assert.That(NavigationPolicy.RequiresSignIn(AppRoutes.Map), Is.False);
        Assert.That(NavigationPolicy.RequiresSignIn(AppRoutes.Account), Is.False);

        // Signed-in roles open these tabs directly; only guests are prompted (BR-AUTH-07).
        Assert.That(NavigationPolicy.CanAccess("Student", AppRoutes.Saved), Is.True);
        Assert.That(NavigationPolicy.CanAccess("Student", AppRoutes.Messages), Is.True);
        Assert.That(NavigationPolicy.CanAccess("Admin", AppRoutes.Saved), Is.True);
        // Guests may still enter the tab; the prompt is shown in place rather than blocking entry.
        Assert.That(NavigationPolicy.CanAccess(null, AppRoutes.Saved), Is.True);
    }

    [Test]
    public void CanAccess_MyListingsAsStudent_False()
    {
        Assert.That(NavigationPolicy.CanAccess("Student", AppRoutes.MyListings), Is.False);
    }

    [Test]
    public void CanAccess_ModerationAsLandlord_False()
    {
        Assert.That(NavigationPolicy.CanAccess("Landlord", AppRoutes.Moderation), Is.False);
    }

    [Test]
    public void CanAccess_AdminReviewAsAdmin_True()
    {
        Assert.That(NavigationPolicy.CanAccess("Admin", AppRoutes.AdminReview), Is.True);
    }
}