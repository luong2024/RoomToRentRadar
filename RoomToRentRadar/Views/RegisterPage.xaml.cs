using RoomToRentRadar.ViewModels;

namespace RoomToRentRadar.Views;

public partial class RegisterPage : ContentPage
{
    public RegisterPage(RegisterViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        RegisterContent.CancelAnimations();
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(26) && !Android.Animation.ValueAnimator.AreAnimatorsEnabled())
            return;
#endif
        RegisterContent.Opacity = 0;
        RegisterContent.TranslationY = 8;
        await Task.WhenAll(RegisterContent.FadeToAsync(1, 200, Easing.CubicOut),
            RegisterContent.TranslateToAsync(0, 0, 200, Easing.CubicOut));
    }

    protected override void OnDisappearing()
    {
        RegisterContent.CancelAnimations();
        RegisterContent.Opacity = 1;
        RegisterContent.TranslationY = 0;
        base.OnDisappearing();
    }
}
