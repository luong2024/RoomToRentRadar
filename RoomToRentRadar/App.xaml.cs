using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Core.Interfaces;

namespace RoomToRentRadar
{
    public partial class App : Application
    {
        private readonly IAuthenticationSessionService _sessionService;
        private readonly IServiceProvider _services;

        public App(IAuthenticationSessionService sessionService, IServiceProvider services)
        {
            InitializeComponent();
            _sessionService = sessionService;
            _services = services;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var appShell = _services.GetRequiredService<AppShell>();
#if WINDOWS
            return new MainWindow(appShell);
#else
            return new Window(appShell);
#endif
        }

        protected override async void OnStart()
        {
            base.OnStart();

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
