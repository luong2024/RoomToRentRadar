using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using RoomToRentRadar.Services.Auth;

namespace RoomToRentRadar;

public partial class App : Application
{
    private const string ThemePreferenceKey = "roomradar.theme";
    private readonly IServiceProvider _services;

    public static event EventHandler? ThemeChanged;

    public App(IServiceProvider services)
    {
        InitializeComponent();
#if ANDROID
        Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.Application.SetWindowSoftInputModeAdjust(
            this, Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.WindowSoftInputModeAdjust.Resize);
#endif
        _services = services;
        var saved = Preferences.Default.Get(ThemePreferenceKey, "Light");
        UserAppTheme = saved == "Dark" ? AppTheme.Dark : AppTheme.Light;
    }

    public static bool IsDarkTheme => Current is { } app &&
        (app.UserAppTheme == AppTheme.Dark || (app.UserAppTheme == AppTheme.Unspecified && app.RequestedTheme == AppTheme.Dark));

    public static void ToggleTheme()
    {
        if (Current is not { } app)
            return;

        var next = IsDarkTheme ? AppTheme.Light : AppTheme.Dark;
        app.UserAppTheme = next;
        Preferences.Default.Set(ThemePreferenceKey, next == AppTheme.Dark ? "Dark" : "Light");
        ThemeChanged?.Invoke(app, EventArgs.Empty);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _ = _services.GetRequiredService<AuthService>().EnsureSignedInAsync();

        var isDark = IsDarkTheme;
        return new Window(_services.GetRequiredService<AppShell>())
        {
            Title = "Room Radar",
            TitleBar = new TitleBar
            {
                Title = "Room Radar",
                BackgroundColor = isDark ? Color.FromArgb("#080E1A") : Color.FromArgb("#F2F6FC"),
                ForegroundColor = isDark ? Color.FromArgb("#F4F8FF") : Color.FromArgb("#0B1526")
            },
            Width = 1200,
            Height = 850,
            MinimumWidth = 360,
            MinimumHeight = 540
        };
    }
}
