using RoomToRentRadar.ViewModels.Home;

namespace RoomToRentRadar.Views.Home;

public partial class HomePage : ContentPage
{
    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
