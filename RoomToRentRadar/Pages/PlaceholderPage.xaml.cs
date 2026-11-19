using RoomToRentRadar.PageModels;
using RoomToRentRadar.Services.Navigation.Core;

namespace RoomToRentRadar.Pages;

/// <summary>
/// One shared page for every placeholder route (T00.15). Detail routes are created by a
/// <see cref="RouteFactory"/> that already knows the catalog entry; the my-listings and moderation
/// tabs are Shell <c>DataTemplate</c> content, so they resolve their entry from the current Shell route.
/// </summary>
public partial class PlaceholderPage : ContentPage
{
    private readonly PlaceholderPageModel _viewModel;

    public PlaceholderPage(PlaceholderPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void Configure(PlaceholderEntry entry) => _viewModel.Configure(entry);

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Tab content is shared, so the page learns which tab it is from the Shell location (plan A1 fallback).
        if (_viewModel.IsConfigured)
        {
            return;
        }

        var route = CurrentShellRoute();
        var entry = route is null ? null : PlaceholderCatalog.Find(route);
        if (entry != null)
        {
            _viewModel.Configure(entry);
        }
    }

    private static string? CurrentShellRoute()
    {
        var location = Shell.Current?.CurrentState?.Location?.ToString();
        if (string.IsNullOrEmpty(location))
        {
            return null;
        }

        var query = location.IndexOf('?');
        if (query >= 0)
        {
            location = location[..query];
        }

        var segments = location.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 ? segments[^1] : null;
    }
}