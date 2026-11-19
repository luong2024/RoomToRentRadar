using RoomToRentRadar.PageModels;

namespace RoomToRentRadar.Pages;

public partial class AccountPlaceholderPage : ContentPage
{
    private readonly AccountPageModel _viewModel;

    public AccountPlaceholderPage(AccountPageModel viewModel)
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
