using RoomToRentRadar.Client.Adapters.Abstractions;

namespace RoomToRentRadar
{
    public partial class App : Microsoft.Maui.Controls.Application
    {
        private readonly ISessionStore? _sessionStore;

        public App(ISessionStore? sessionStore = null)
        {
            InitializeComponent();
            _sessionStore = sessionStore;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell(_sessionStore));
        }
    }
}
