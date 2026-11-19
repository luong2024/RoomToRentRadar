using RoomToRentRadar.Features.Favourites.ViewModels;

namespace RoomToRentRadar.Features.Favourites.Views;

public partial class FavouritesPage : ContentPage
{
    private readonly FavouritesViewModel _viewModel;

    public FavouritesPage(FavouritesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.LoadCommand.CanExecute(null))
        {
            _viewModel.LoadCommand.Execute(null);
        }
    }

    protected override void OnDisappearing()
    {
        _viewModel.CancelUndoWindow();
        base.OnDisappearing();
    }

    private async void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is FavouriteListingItem item)
        {
            await _viewModel.OpenCommand.ExecuteAsync(item);
        }
    }

    private async void OnRemoveClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: FavouriteListingItem item })
        {
            await _viewModel.RemoveCommand.ExecuteAsync(item);
        }
    }
}
