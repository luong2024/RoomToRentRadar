using RoomToRentRadar.Features.Home.ViewModels;

namespace RoomToRentRadar.Features.Home.Views;

public partial class HomePage : ContentPage
{
    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
