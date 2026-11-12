using RoomToRentRadar.Features.RoomDetail.ViewModels;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Features.RoomDetail.Views;

public partial class RoomDetailPage : ContentPage
{
    private readonly RoomDetailViewModel _viewModel;

    public RoomDetailPage(RoomDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_viewModel.State == RoomDetailState.Initial && _viewModel.LoadCommand.CanExecute(null))
        {
            _viewModel.LoadCommand.Execute(null);
        }
        else if (_viewModel.State == RoomDetailState.Content
                 && _viewModel.RefreshContactCommand.CanExecute(null))
        {
            // Returning from the sign-in page must reveal the phone without a reload.
            _viewModel.RefreshContactCommand.Execute(null);
        }
    }

    private async void OnSimilarRoomTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is ListingSummaryDto listing
            && _viewModel.OpenSimilarRoomCommand.CanExecute(listing))
        {
            await _viewModel.OpenSimilarRoomCommand.ExecuteAsync(listing);
        }
    }
}
