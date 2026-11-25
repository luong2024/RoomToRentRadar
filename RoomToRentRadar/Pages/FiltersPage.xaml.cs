namespace RoomToRentRadar.Pages;

public partial class FiltersPage : ContentPage
{
    private readonly PageModels.FiltersPageModel _viewModel;

    public FiltersPage(PageModels.FiltersPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        Unloaded += (_, _) => _viewModel.Dispose();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
