using System.Globalization;
using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Core.Localization;
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
        var initial = LocalizationResourceManager.Instance.CurrentCulture;
        try
        {
            LocalizationResourceManager.Instance.SetCulture(new CultureInfo("vi"));
            var vm = new HomeViewModel();

            // Assert
            Assert.That(vm.Subtitle, Is.EqualTo("Tìm phòng trọ lý tưởng quanh ĐH CNTT&TT (ICTU)"));
        }
        finally
        {
            LocalizationResourceManager.Instance.SetCulture(initial);
        }
    }

    [Test]
    public void CultureChange_WhenSwitchedToEnglish_UpdatesTitleAndSubtitle()
    {
        // Arrange
        var initial = LocalizationResourceManager.Instance.CurrentCulture;
        try
        {
            LocalizationResourceManager.Instance.SetCulture(new CultureInfo("vi"));
            var vm = new HomeViewModel();
            Assert.That(vm.Subtitle, Is.EqualTo("Tìm phòng trọ lý tưởng quanh ĐH CNTT&TT (ICTU)"));

            // Act
            LocalizationResourceManager.Instance.SetCulture(new CultureInfo("en"));

            // Assert
            Assert.That(vm.Subtitle, Is.EqualTo("Find your ideal room around ICTU campus"));
            Assert.That(vm.Title, Is.EqualTo("RoomToRentRadar"));
        }
        finally
        {
            LocalizationResourceManager.Instance.SetCulture(initial);
        }
    }

    [Test]
    public void HeaderCommands_CanExecuteWithoutExceptions()
    {
        // Arrange
        var vm = new HomeViewModel();

        // Act & Assert
        Assert.DoesNotThrow(() =>
        {
            vm.OpenMenuCommand.Execute(null);
            vm.OpenNotificationsCommand.Execute(null);
            vm.OpenProfileCommand.Execute(null);
        });
    }
}
