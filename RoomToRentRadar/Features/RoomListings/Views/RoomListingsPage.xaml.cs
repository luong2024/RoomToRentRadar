using System.ComponentModel;
using RoomToRentRadar.Features.RoomListings.ViewModels;

namespace RoomToRentRadar.Features.RoomListings.Views;

public partial class RoomListingsPage : ContentPage
{
    private readonly RoomListingsViewModel _viewModel;

    public RoomListingsPage(RoomListingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        RootPageGrid.SizeChanged += OnRootPageGridSizeChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeCommand.ExecuteAsync(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.IsPriceMenuOpen = false;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoomListingsViewModel.IsPriceMenuOpen) && _viewModel.IsPriceMenuOpen)
        {
            Dispatcher.Dispatch(UpdatePriceMenuPosition);
        }
    }

    private void OnRootPageGridSizeChanged(object? sender, EventArgs e)
    {
        if (_viewModel.IsPriceMenuOpen)
        {
            UpdatePriceMenuPosition();
        }
    }

    private void UpdatePriceMenuPosition()
    {
        if (RootPageGrid.Height > 0)
        {
            PriceDropdownMenu.HeightRequest = Math.Clamp(RootPageGrid.Height * 0.50, 320, 480);
        }
    }
}
