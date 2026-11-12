using RoomToRentRadar.Features.Login.ViewModels;

namespace RoomToRentRadar.Features.Login.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        LoginContent.CancelAnimations();
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(26) && !Android.Animation.ValueAnimator.AreAnimatorsEnabled())
            return;
#endif
        LoginContent.Opacity = 0;
        LoginContent.TranslationY = 8;
        await Task.WhenAll(LoginContent.FadeToAsync(1, 200, Easing.CubicOut),
            LoginContent.TranslateToAsync(0, 0, 200, Easing.CubicOut));
    }

    protected override void OnDisappearing()
    {
        LoginContent.CancelAnimations();
        LoginContent.Opacity = 1;
        LoginContent.TranslationY = 0;
        base.OnDisappearing();
    }
}
