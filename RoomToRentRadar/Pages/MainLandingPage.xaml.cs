using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.Pages;

public partial class MainLandingPage : ContentPage
{
    private static readonly string[] SortKeys = { "distance", "price_asc", "price_desc", "newest" };

    private readonly PageModels.MainLandingPageModel _viewModel;

    public MainLandingPage(PageModels.MainLandingPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.SortSheetRequested = ShowSortSheetAsync;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }

    private async Task ShowSortSheetAsync()
    {
        var options = new[]
        {
            AppResources.GetString("Sort_Distance"),
            AppResources.GetString("Sort_PriceAsc"),
            AppResources.GetString("Sort_PriceDesc"),
            AppResources.GetString("Sort_Newest")
        };

        var choice = await Shell.Current.DisplayActionSheetAsync(
            AppResources.GetString("Filters_SortBy"),
            AppResources.GetString("Common_Cancel"),
            null,
            options);

        if (string.IsNullOrEmpty(choice))
            return;

        var index = Array.IndexOf(options, choice);
        if (index >= 0)
            _viewModel.ApplySortCommand.Execute(SortKeys[index]);
    }
}
