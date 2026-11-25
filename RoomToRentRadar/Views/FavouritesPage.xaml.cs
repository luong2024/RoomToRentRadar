using RoomToRentRadar.Helpers;
using RoomToRentRadar.ViewModels;

namespace RoomToRentRadar.Views;

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

    private async void OnFavouriteTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is FavouriteRoomItem item
            && _viewModel.OpenRoomCommand.CanExecute(item))
        {
            await _viewModel.OpenRoomCommand.ExecuteAsync(item);
        }
    }

    private async void OnRemoveTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is FavouriteRoomItem item
            && _viewModel.RemoveCommand.CanExecute(item))
        {
            MotionEffects.TryLightHaptic();
            await _viewModel.RemoveCommand.ExecuteAsync(item);
        }
    }
}
