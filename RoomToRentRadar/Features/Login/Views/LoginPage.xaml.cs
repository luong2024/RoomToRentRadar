using RoomToRentRadar.Features.Login.ViewModels;

namespace RoomToRentRadar.Features.Login.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
