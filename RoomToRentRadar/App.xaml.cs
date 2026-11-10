namespace RoomToRentRadar;

public partial class App : Application
{
    public App(AppShell appShell)
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Light;
        _appShell = appShell;
    }

    private readonly AppShell _appShell;

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_appShell)
        {
            Title = "Room Radar",
            TitleBar = new TitleBar
            {
                Title = "Room Radar",
                BackgroundColor = Colors.White,
                ForegroundColor = Color.FromArgb("#252E2A")
            },
            Width = 1200,
            Height = 850,
            MinimumWidth = 360,
            MinimumHeight = 540
        };
    }
}
