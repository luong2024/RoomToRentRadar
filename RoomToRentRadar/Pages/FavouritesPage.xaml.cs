using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using RoomToRentRadar.Models;
using RoomToRentRadar.PageModels;

namespace RoomToRentRadar.Pages;

public partial class FavouritesPage : ContentPage
{
    private readonly FavouritesViewModel _viewModel;

    public FavouritesViewModel ViewModel => _viewModel;

    public FavouritesPage(FavouritesViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadFavouritesAsync();
    }

    private async void OnFavouriteSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not RoomListingItemModel selected)
            return;

        if (sender is CollectionView cv)
        {
            cv.SelectedItem = null;
        }

        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("//room-detail", new Dictionary<string, object>
            {
                ["id"] = selected.Id
            });
        }
    }
}
