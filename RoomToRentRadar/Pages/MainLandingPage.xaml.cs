namespace RoomToRentRadar.Pages;

public partial class MainLandingPage : ContentPage
{
    private readonly PageModels.MainLandingPageModel _viewModel;

    public MainLandingPage(PageModels.MainLandingPageModel viewModel)
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

