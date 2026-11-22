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
        _viewModel.IsFilterPanelOpen = false;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if ((e.PropertyName == nameof(RoomListingsViewModel.IsPriceMenuOpen) ||
             e.PropertyName == nameof(RoomListingsViewModel.IsFilterPanelOpen)) &&
            (_viewModel.IsPriceMenuOpen || _viewModel.IsFilterPanelOpen))
        {
            Dispatcher.Dispatch(UpdatePriceMenuPosition);
        }
    }

    private void OnRootPageGridSizeChanged(object? sender, EventArgs e)
    {
        if (_viewModel.IsPriceMenuOpen || _viewModel.IsFilterPanelOpen)
        {
            UpdatePriceMenuPosition();
        }
    }

    private void UpdatePriceMenuPosition()
    {
        if (RootPageGrid.Height > 0)
        {
            FilterPanel.HeightRequest = Math.Clamp(RootPageGrid.Height * 0.55, 340, 520);
        }
    }
}
