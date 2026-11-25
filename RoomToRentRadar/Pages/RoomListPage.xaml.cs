namespace RoomToRentRadar.Pages;

public partial class RoomListPage : ContentPage
{
    private readonly RoomListViewModel _viewModel;

    public RoomListPage(RoomListViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.Listings.Count == 0 && !_viewModel.IsLoading)
        {
            await _viewModel.LoadInitialListingsAsync();
        }
    }
}