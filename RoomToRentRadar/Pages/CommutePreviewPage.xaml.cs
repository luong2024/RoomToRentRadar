using Microsoft.Maui.Controls;
using RoomToRentRadar.PageModels;

namespace RoomToRentRadar.Pages;

public partial class CommutePreviewPage : ContentPage
{
    private readonly CommuteViewModel _viewModel;

    public CommutePreviewPage(CommuteViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommuteAsync();
    }
}
