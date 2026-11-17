using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Core.Interfaces;

namespace RoomToRentRadar
{
    public partial class App : Application
    {
        private readonly IServiceProvider? _serviceProvider;
        private readonly IAuthenticationSessionService? _sessionService;
        private Task<bool>? _startupRestoreTask;

        public App(IServiceProvider serviceProvider, IAuthenticationSessionService sessionService)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;
            _sessionService = sessionService;
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
            var appShell = _serviceProvider?.GetService<AppShell>() ?? new AppShell();
            return new MainWindow(appShell);
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
