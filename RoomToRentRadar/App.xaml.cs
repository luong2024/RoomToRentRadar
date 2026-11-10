using Microsoft.Extensions.DependencyInjection;

namespace RoomToRentRadar
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            UserAppTheme = AppTheme.Light;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell())
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
}
