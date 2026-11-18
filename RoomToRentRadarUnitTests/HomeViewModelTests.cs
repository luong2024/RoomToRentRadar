using RoomToRentRadar.Features.Home.ViewModels;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class HomeViewModelTests
{
    [Test]
    public async Task NavigateToAuthAsync_UsesTheUniqueAuthenticationShellRoute()
    {
        string? navigatedRoute = null;
        var viewModel = new HomeViewModel(route =>
        {
            navigatedRoute = route;
            return Task.CompletedTask;
        });

        await viewModel.NavigateToAuthAsync();

        Assert.That(navigatedRoute, Is.EqualTo("//auth/login"));
    }
}
