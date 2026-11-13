using RoomToRentRadar.Features.RoomDetails.ViewModels;

namespace RoomToRentRadar.Features.RoomDetails.Views;

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
        if (_viewModel.Room is null)
        {
            _viewModel.LoadRoomDetailsCommand.Execute(null);
        }
    }
}