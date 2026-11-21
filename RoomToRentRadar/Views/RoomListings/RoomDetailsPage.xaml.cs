using RoomToRentRadar.ViewModels.RoomListings;

namespace RoomToRentRadar.Views.RoomListings;

public partial class RoomDetailsPage : ContentPage
{
    public RoomDetailsPage(RoomDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
