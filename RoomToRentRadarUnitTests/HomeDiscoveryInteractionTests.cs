using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Features.Home.ViewModels;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadarUnitTests;

public partial class HomeViewModelTests
{
    [Test]
    public async Task ResetDraft_WhenFiltersApplied_DoesNotChangeResultsUntilApply()
    {
        var api = new FakeApiClient(CreatePage(null, CreateListing()), CreatePage(null, CreateListing()));
        var model = new HomeViewModel(api, new FakeErrorHandler(), new FakeNavigationService(), new TestFavouritesStore());
        model.PriceMinText = "2";
        model.PriceMaxText = "4";
        await model.ApplyFiltersCommand.ExecuteAsync(null);
        var filteredIds = model.Listings.Select(item => item.Id).ToArray();
        model.ToggleFilterPanelCommand.Execute(null);

        model.ResetDraftCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(model.PriceMinText, Is.Empty);
            Assert.That(model.PriceMaxText, Is.Empty);
            Assert.That(model.IsFilterPanelOpen, Is.True);
            Assert.That(model.ActiveFilterCount, Is.EqualTo(1));
            Assert.That(model.Listings.Select(item => item.Id), Is.EqualTo(filteredIds));
            Assert.That(api.RequestedFilters, Has.Count.EqualTo(1));
        });

        await model.ApplyFiltersCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(api.RequestedFilters, Has.Count.EqualTo(2));
            Assert.That(api.RequestedFilters[^1], Is.Null);
            Assert.That(model.ActiveFilterCount, Is.Zero);
            Assert.That(model.IsFilterPanelOpen, Is.False);
        });
    }

    [Test]
    public async Task CloseFilterPanel_WithUnsavedEdits_RestoresAppliedValuesOnReopen()
    {
        var api = new FakeApiClient(CreatePage(null, CreateListing()));
        var model = new HomeViewModel(api, new FakeErrorHandler(), new FakeNavigationService(), new TestFavouritesStore());
        model.PriceMinText = "2";
        model.AreaMinText = "20";
        model.RoomTypeChips.Single(chip => chip.Value is RoomType.Studio).SelectCommand.Execute(null);
        model.CampusChips.Single(chip => Equals(chip.Value, HomeFilterOptions.Campuses[0])).SelectCommand.Execute(null);
        model.DistanceChips.Single(chip => Equals(chip.Value, 5)).SelectCommand.Execute(null);
        await model.ApplyFiltersCommand.ExecuteAsync(null);
        model.ToggleFilterPanelCommand.Execute(null);
        model.ResetDraftCommand.Execute(null);
        model.PriceMaxText = "1";

        model.ToggleFilterPanelCommand.Execute(null);
        model.ToggleFilterPanelCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(model.PriceMinText, Is.EqualTo("2"));
            Assert.That(model.PriceMaxText, Is.Empty);
            Assert.That(model.AreaMinText, Is.EqualTo("20"));
            Assert.That(model.RoomTypeChips.Single(chip => chip.IsSelected).Value, Is.EqualTo(RoomType.Studio));
            Assert.That(model.CampusChips.Single(chip => chip.IsSelected).Value, Is.EqualTo(HomeFilterOptions.Campuses[0]));
            Assert.That(model.DistanceChips.Single(chip => chip.IsSelected).Value, Is.EqualTo(5));
            Assert.That(model.ActiveFilterCount, Is.EqualTo(4));
            Assert.That(api.RequestedFilters, Has.Count.EqualTo(1));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public async Task RemoveFilterChip_RemovesOnlyItsAppliedGroup(int group)
    {
        var api = new FakeApiClient(CreatePage(null, CreateListing()), CreatePage(null, CreateListing()));
        var model = new HomeViewModel(api, new FakeErrorHandler(), new FakeNavigationService(), new TestFavouritesStore());
        model.PriceMinText = "2";
        model.PriceMaxText = "4";
        model.AreaMinText = "20";
        model.AreaMaxText = "35";
        model.RoomTypeChips.Single(chip => chip.Value is RoomType.Studio).SelectCommand.Execute(null);
        var campus = HomeFilterOptions.Campuses[0];
        model.CampusChips.Single(chip => Equals(chip.Value, campus)).SelectCommand.Execute(null);
        await model.ApplyFiltersCommand.ExecuteAsync(null);

        await ((IAsyncRelayCommand)model.ActiveFilterChips[group].RemoveCommand!).ExecuteAsync(null);

        var expected = new ActiveListingFiltersDto(
            group == 0 ? null : 2_000_000,
            group == 0 ? null : 4_000_000,
            group == 1 ? null : 20,
            group == 1 ? null : 35,
            null,
            group == 2 ? null : RoomType.Studio,
            group == 3 ? null : campus.Lat,
            group == 3 ? null : campus.Lng,
            group == 3 ? null : 3);
        Assert.Multiple(() =>
        {
            Assert.That(api.RequestedFilters, Has.Count.EqualTo(2));
            Assert.That(api.RequestedFilters[^1], Is.EqualTo(expected));
            Assert.That(api.RequestedCursors[^1], Is.Null);
            Assert.That(model.ActiveFilterCount, Is.EqualTo(3));
            Assert.That(model.ActiveFilterChips, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public async Task ActiveFilterCount_GroupsBoundsAndCampusRadiusRatherThanIndividualFields()
    {
        var api = new FakeApiClient(CreatePage(null, CreateListing()));
        var model = new HomeViewModel(api, new FakeErrorHandler(), new FakeNavigationService(), new TestFavouritesStore());
        model.PriceMinText = "1";
        model.PriceMaxText = "5";
        model.AreaMinText = "10";
        model.AreaMaxText = "50";
        model.CampusChips.Single(chip => Equals(chip.Value, HomeFilterOptions.Campuses[1])).SelectCommand.Execute(null);
        await model.ApplyFiltersCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(model.ActiveFilterCount, Is.EqualTo(3));
            Assert.That(model.FilterButtonText, Does.Contain("3"));
        });
    }

    [Test]
    public async Task SelectSearchCampus_PreservesAppliedFiltersAndDiscardsUnappliedDraft()
    {
        var api = new FakeApiClient(CreatePage(null, CreateListing()), CreatePage(null, CreateListing()));
        var model = new HomeViewModel(api, new FakeErrorHandler(), new FakeNavigationService(), new TestFavouritesStore());
        model.PriceMinText = "2";
        model.AreaMaxText = "35";
        await model.ApplyFiltersCommand.ExecuteAsync(null);
        model.PriceMinText = "9";
        model.OpenSearchCommand.Execute(null);
        var campus = HomeFilterOptions.Campuses[1];

        await model.SelectSearchCampusCommand.ExecuteAsync(campus);

        Assert.Multiple(() =>
        {
            Assert.That(api.RequestedFilters, Has.Count.EqualTo(2));
            Assert.That(api.RequestedFilters[^1], Is.EqualTo(new ActiveListingFiltersDto(
                2_000_000, null, null, 35, null, null, campus.Lat, campus.Lng, 3)));
            Assert.That(model.ActiveFilterCount, Is.EqualTo(3));
        });
    }

    [Test]
    public void SearchQuery_WithNoMatchingCampus_ReturnsNoSuggestionsWithoutNetworkRequest()
    {
        var api = new FakeApiClient();
        var model = new HomeViewModel(api, new FakeErrorHandler(), new FakeNavigationService(), new TestFavouritesStore());
        model.OpenSearchCommand.Execute(null);

        model.SearchQuery = "no matching place 9876";

        Assert.Multiple(() =>
        {
            Assert.That(model.CampusSuggestions, Is.Empty);
            Assert.That(api.RequestedFilters, Is.Empty);
        });
    }
}
