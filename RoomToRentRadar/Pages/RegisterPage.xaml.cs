using RoomToRentRadar.PageModels;

namespace RoomToRentRadar.Pages;

public partial class RegisterPage : ContentPage
{
    public RegisterPage(RegisterPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

