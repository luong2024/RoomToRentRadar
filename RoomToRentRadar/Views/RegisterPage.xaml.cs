using RoomToRentRadar.Helpers;
using RoomToRentRadar.ViewModels;

namespace RoomToRentRadar.Views;

public partial class RegisterPage : ContentPage
{
    public RegisterPage(RegisterViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = MotionEffects.FadeSlideInAsync(RegisterContent, 10, 200);
    }

    protected override void OnDisappearing()
    {
        RegisterContent.CancelAnimations();
        RegisterContent.Opacity = 1;
        RegisterContent.TranslationY = 0;
        base.OnDisappearing();
    }
}
