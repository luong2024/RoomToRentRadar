using RoomToRentRadar.PageModels;

namespace RoomToRentRadar.Pages;

public partial class RoomDetailPage : ContentPage
{
    private readonly RoomDetailPageModel _viewModel;

    public RoomDetailPage(RoomDetailPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
