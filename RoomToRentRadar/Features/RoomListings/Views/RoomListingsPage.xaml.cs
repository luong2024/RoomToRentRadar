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
        double x = 0;
        double y = 0;
        Element? current = PriceDropdownButton;

        while (current is VisualElement ve && !ReferenceEquals(ve, RootPageGrid))
        {
            x += ve.X;
            y += ve.Y;

            if (ve.Parent is ScrollView scroll)
            {
                x -= scroll.ScrollX;
                y -= scroll.ScrollY;
            }

            current = ve.Parent;
        }

        double menuWidth = PriceDropdownMenu.WidthRequest > 0 ? PriceDropdownMenu.WidthRequest : 210;
        double maxLeft = Math.Max(8, RootPageGrid.Width - menuWidth - 12);
        double clampedLeft = Math.Clamp(x, 8, maxLeft);
        double top = y + PriceDropdownButton.Height + 6;

        PriceDropdownMenu.Margin = new Thickness(clampedLeft, top, 0, 0);
    }
}
