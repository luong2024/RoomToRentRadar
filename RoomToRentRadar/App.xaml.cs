using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Core.Interfaces;

namespace RoomToRentRadar
{
    public partial class App : Application
    {
        private readonly IAuthenticationSessionService? _sessionService;

        public App(IAuthenticationSessionService sessionService)
        {
            InitializeComponent();
            _sessionService = sessionService;
        }

        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }

        protected override async void OnStart()
        {
            base.OnStart();

            if (_sessionService != null)
            {
                try
                {
                    await _sessionService.TryRestoreSessionAsync();
                }
                catch
                {
                    // Fail-safe suppression on startup restore
                }
            }
        }
    }
}
