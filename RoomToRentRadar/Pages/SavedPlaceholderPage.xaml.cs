using RoomToRentRadar.PageModels;

namespace RoomToRentRadar.Pages;

public partial class SavedPlaceholderPage : ContentPage
{
    private readonly SavedPageModel _viewModel;

    public SavedPlaceholderPage(SavedPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Refresh();
    }
}