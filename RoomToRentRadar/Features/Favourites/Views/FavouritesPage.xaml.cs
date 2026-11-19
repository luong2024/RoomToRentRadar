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

        // Refreshing on every appearance keeps prices and availability current
        // and pulls in favourites saved from other devices.
        if (_viewModel.LoadCommand.CanExecute(null))
        {
            _viewModel.LoadCommand.Execute(null);
        }
    }

    private async void OnFavouriteTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is FavouriteRoomItem item
            && _viewModel.OpenRoomCommand.CanExecute(item))
        {
            await _viewModel.OpenRoomCommand.ExecuteAsync(item);
        }
    }

    private async void OnRemoveClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: FavouriteRoomItem item }
            && _viewModel.RemoveCommand.CanExecute(item))
        {
            await _viewModel.RemoveCommand.ExecuteAsync(item);
        }
    }
}
