using RoomToRentRadar.PageModels;

namespace RoomToRentRadar.Pages;

public partial class RoomDetailPage : ContentPage
{
    private readonly RoomDetailPageModel _viewModel;

    public RoomDetailPage(RoomDetailPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        Unloaded += OnPageUnloaded;
    }

    private void OnPageUnloaded(object? sender, EventArgs e)
    {
        Unloaded -= OnPageUnloaded;
        _viewModel.Dispose();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
