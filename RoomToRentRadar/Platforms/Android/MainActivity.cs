using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace RoomToRentRadar;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ApplySystemBarAppearance();
        App.ThemeChanged += OnAppThemeChanged;
    }

    protected override void OnDestroy()
    {
        App.ThemeChanged -= OnAppThemeChanged;
        base.OnDestroy();
    }

    private void OnAppThemeChanged(object? sender, EventArgs e)
        => RunOnUiThread(ApplySystemBarAppearance);

    private void ApplySystemBarAppearance()
    {
        if (Window is null)
            return;

        var isDark = App.IsDarkTheme;
        var statusHex = isDark ? "#080E1A" : "#EEF5FF";
        var navHex = isDark ? "#0D1729" : "#F5F9FF";

#pragma warning disable CA1422
        Window.SetStatusBarColor(Android.Graphics.Color.ParseColor(statusHex));
        Window.SetNavigationBarColor(Android.Graphics.Color.ParseColor(navHex));
#pragma warning restore CA1422

        var controller = WindowCompat.GetInsetsController(Window, Window.DecorView);
        if (controller is not null)
        {
            controller.AppearanceLightStatusBars = !isDark;
            controller.AppearanceLightNavigationBars = !isDark;
        }
    }
}
