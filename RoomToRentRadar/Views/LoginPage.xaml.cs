using RoomToRentRadar.Helpers;
using RoomToRentRadar.ViewModels;

namespace RoomToRentRadar.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = MotionEffects.FadeSlideInAsync(LoginContent, 10, 200);
    }

    protected override void OnDisappearing()
    {
        LoginContent.CancelAnimations();
        LoginContent.Opacity = 1;
        LoginContent.TranslationY = 0;
        base.OnDisappearing();
    }
}
