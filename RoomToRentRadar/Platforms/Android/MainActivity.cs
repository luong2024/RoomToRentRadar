using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace RoomToRentRadar
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            if (Window is not null)
            {
#pragma warning disable CA1422
                Window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#FFFFFF"));
                Window.SetNavigationBarColor(Android.Graphics.Color.ParseColor("#FFFFFF"));
#pragma warning restore CA1422
                var controller = WindowCompat.GetInsetsController(Window, Window.DecorView);
                if (controller is not null)
                {
                    controller.AppearanceLightStatusBars = true;
                    controller.AppearanceLightNavigationBars = true;
                }
            }
        }
    }
}
