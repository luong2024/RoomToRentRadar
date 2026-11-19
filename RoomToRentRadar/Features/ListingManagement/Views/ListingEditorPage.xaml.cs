using RoomToRentRadar.Features.ListingManagement.ViewModels;

namespace RoomToRentRadar.Features.ListingManagement.Views;

public partial class ListingEditorPage : ContentPage
{
    private readonly ListingEditorViewModel _viewModel;

    public ListingEditorPage(ListingEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Shell does not guarantee that IQueryAttributable ran before OnAppearing,
        // so the view model guards against a double load.
        if (!_viewModel.IsBusy && _viewModel.LoadCommand.CanExecute(null))
            _viewModel.LoadCommand.Execute(null);
    }
}
