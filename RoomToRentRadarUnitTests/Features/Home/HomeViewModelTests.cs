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
    public void Subtitle_ByDefault_MentionsIctuWelcomeHeading()
    {
        // Arrange
        var vm = new HomeViewModel();

        // Assert
        Assert.That(vm.Subtitle, Is.EqualTo("Tìm phòng trọ lý tưởng quanh ĐH CNTT&TT (ICTU)"));
    }
}
