using RoomToRentRadar.PageModels;

namespace RoomToRentRadar.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

