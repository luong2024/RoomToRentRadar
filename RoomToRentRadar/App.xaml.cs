using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Core.Interfaces;

namespace RoomToRentRadar
{
    public partial class App : Application
    {
        private readonly IAuthenticationSessionService? _sessionService;
        private readonly IServiceProvider? _services;
        private Task<bool>? _startupRestoreTask;

        public App(IAuthenticationSessionService sessionService, IServiceProvider services)
        {
            InitializeComponent();
            _sessionService = sessionService;
            _services = services;
        }

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
            _ = TriggerSessionRestoreSafeAsync();
            var appShell = _services != null
                ? _services.GetRequiredService<AppShell>()
                : new AppShell();
#if WINDOWS
            return new MainWindow(appShell);
#else
            return new Window(appShell);
#endif
        }

        protected override async void OnStart()
        {
            base.OnStart();
            await TriggerSessionRestoreSafeAsync();
        }

        private async Task TriggerSessionRestoreSafeAsync()
        {
            if (_sessionService == null)
            {
                return;
            }

            try
            {
                _startupRestoreTask ??= _sessionService.TryRestoreSessionAsync();
                await _startupRestoreTask;
            }
            catch
            {
                // Fail-safe suppression on startup restore
            }
        }
    }
}
