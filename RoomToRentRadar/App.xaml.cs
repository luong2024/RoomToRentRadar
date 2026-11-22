using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Services.Auth;

namespace RoomToRentRadar;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
#if ANDROID
        Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.Application.SetWindowSoftInputModeAdjust(
            this, Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.WindowSoftInputModeAdjust.Resize);
#endif
        UserAppTheme = AppTheme.Light;
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _ = _services.GetRequiredService<AuthService>().EnsureSignedInAsync();

        // Resolved here so the Shell is built after App.xaml resource dictionaries load.
        return new Window(_services.GetRequiredService<AppShell>())
        {
            Title = "Room Radar",
            TitleBar = new TitleBar
            {
                Title = "Room Radar",
                BackgroundColor = Colors.White,
                ForegroundColor = (Color)Resources["Ink"]
            },
            Width = 1200,
            Height = 850,
            MinimumWidth = 360,
            MinimumHeight = 540
        };
    }
}
