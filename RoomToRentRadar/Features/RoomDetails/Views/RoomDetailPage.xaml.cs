using RoomToRentRadar.Features.RoomDetails.ViewModels;

namespace RoomToRentRadar.Features.RoomDetails.Views;

public partial class RoomDetailPage : ContentPage
{
    public RoomDetailPage(RoomDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
