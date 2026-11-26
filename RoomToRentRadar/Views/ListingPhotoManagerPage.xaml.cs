using RoomToRentRadar.ViewModels;

namespace RoomToRentRadar.Views;

public partial class ListingPhotoManagerPage : ContentPage
{
    private readonly ListingPhotoManagerViewModel _viewModel;
    private bool _loaded;

    public ListingPhotoManagerPage(ListingPhotoManagerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.Lightbox.IsOpen)
        {
            _viewModel.Lightbox.CloseCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_loaded || !_viewModel.LoadCommand.CanExecute(null))
            return;

        _loaded = true;
        _viewModel.LoadCommand.Execute(null);
    }
}
