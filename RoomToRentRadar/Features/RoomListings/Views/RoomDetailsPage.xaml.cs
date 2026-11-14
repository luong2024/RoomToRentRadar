using RoomToRentRadar.Features.RoomListings.ViewModels;

namespace RoomToRentRadar.Features.RoomListings.Views;

public partial class RoomDetailsPage : ContentPage
{
    public RoomDetailsPage(RoomDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
