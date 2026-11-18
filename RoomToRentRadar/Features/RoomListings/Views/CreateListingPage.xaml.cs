using RoomToRentRadar.Features.RoomListings.ViewModels;

namespace RoomToRentRadar.Features.RoomListings.Views;

public partial class CreateListingPage : ContentPage
{
    public CreateListingPage(CreateListingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
