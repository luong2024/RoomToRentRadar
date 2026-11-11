using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Features.Home.ViewModels;

namespace RoomToRentRadarUnitTests.Features.Home;

[TestFixture]
public class HomeViewModelTests
{
    [Test]
    public async Task ExploreRoomsCommand_WhenExecuted_NavigatesToRoomsRoute()
    {
        // Arrange
        var navigatedRoutes = new List<string>();
        var vm = new HomeViewModel(route =>
        {
            navigatedRoutes.Add(route);
            return Task.CompletedTask;
        });

        // Act
        await vm.ExploreRoomsCommand.ExecuteAsync(null);

        // Assert
        Assert.That(navigatedRoutes, Is.EqualTo(new[] { $"//{AppRoutes.RoomListings}" }));
    }

    [Test]
    public async Task ExploreRoomsCommand_WhenQuickSearchTextEntered_ForwardsEncodedKeywordToRoomsRoute()
    {
        // Arrange
        var navigatedRoutes = new List<string>();
        var vm = new HomeViewModel(route =>
        {
            navigatedRoutes.Add(route);
            return Task.CompletedTask;
        })
        {
            QuickSearchText = "  Điều hòa Z115  "
        };

        // Act
        await vm.ExploreRoomsCommand.ExecuteAsync(null);
        await vm.SearchFromHomeCommand.ExecuteAsync(null);

        // Assert
        var expectedRoute = $"//{AppRoutes.RoomListings}?search={Uri.EscapeDataString("Điều hòa Z115")}";
        Assert.That(navigatedRoutes, Is.EqualTo(new[] { expectedRoute, expectedRoute }));
    }

    [Test]
    public void Subtitle_ByDefault_MentionsIctuWelcomeHeading()
    {
        // Arrange
        var vm = new HomeViewModel();

        // Assert
        Assert.That(vm.Subtitle, Is.EqualTo("Tìm phòng trọ lý tưởng quanh ĐH CNTT&TT (ICTU)"));
    }
}
