using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Services.Navigation.Core;
using Font = Microsoft.Maui.Font;

namespace RoomToRentRadar
{
    public partial class AppShell : Shell
    {
        private readonly ISessionStore? _sessionStore;
        private readonly IAppSession? _appSession;

        public AppShell(ISessionStore? sessionStore = null, IAppSession? appSession = null)
        {
            _sessionStore = sessionStore;
            // AppShell is created manually by App.CreateWindow, so fall back to platform DI (T00.15).
            _appSession = appSession ?? IPlatformApplication.Current?.Services.GetService<IAppSession>();
            InitializeComponent();

            // Register modal/push routes
            Routing.RegisterRoute(AppRoutes.Register, typeof(Pages.RegisterPage));
            Routing.RegisterRoute(AppRoutes.RoleSelection, typeof(Pages.RoleSelectionPage));

            // Register the 12 shared placeholder detail routes (T00.15 P4).
            AppRoutes.RegisterAll();

            // Register real FiltersPage (US04 / T04.2) over the placeholder route.
            Routing.RegisterRoute(AppRoutes.SearchFilters, typeof(Pages.FiltersPage));

            // Register real RoomDetailPage (US02 / T02.2)
            Routing.RegisterRoute(AppRoutes.RoomDetail, typeof(Pages.RoomDetailPage));
            Routing.RegisterRoute("roomdetail", typeof(Pages.RoomDetailPage));

            if (_appSession != null)
            {
                _appSession.SessionEnded += OnSessionEnded;
            }

#if !DEBUG
            // Hide UI Design System Catalog in production Release builds (M5)
            if (UiCatalogContent != null && Items.Contains(UiCatalogContent))
            {
                Items.Remove(UiCatalogContent);
            }
#endif

            Loaded += OnAppShellLoaded;
        }

        private async void OnAppShellLoaded(object? sender, EventArgs e)
        {
            Loaded -= OnAppShellLoaded;
            await CheckInitialSessionAsync();
        }

        private void OnSessionEnded(object? sender, EventArgs e)
        {
            // SessionExpired fires from the thread pool; navigation must run on the UI thread (T00.15 AC-2).
            MainThread.BeginInvokeOnMainThread(async () => await GoToAsync("//" + AppRoutes.Explore));
        }

        private async Task CheckInitialSessionAsync()
        {
            if (_sessionStore == null) return;

            try
            {
                if (_appSession != null)
                {
                    await _appSession.RefreshAsync();
                }

                // App starts at //explore (Explore) by default to support Guest Browsing (docs/ui-ux-spec.md §1 Principle 7).
                // If the user already has a valid token, we stay on //explore.
                // If on login route with a valid token, redirect to //explore.
                bool hasValid = await _sessionStore.HasValidTokenAsync();
                if (hasValid && CurrentState?.Location?.ToString()?.Contains("login") == true)
                {
                    await GoToAsync("//" + AppRoutes.Explore);
                }
            }
            catch
            {
                // Silently keep current route
            }
        }

        public static async Task DisplaySnackbarAsync(string message)
        {
            var cancellationTokenSource = new CancellationTokenSource();

            var snackbarOptions = new SnackbarOptions
            {
                BackgroundColor = Color.FromArgb("#FF3300"),
                TextColor = Colors.White,
                ActionButtonTextColor = Colors.Yellow,
                CornerRadius = new CornerRadius(0),
                Font = Font.SystemFontOfSize(18),
                ActionButtonFont = Font.SystemFontOfSize(14)
            };

            var snackbar = Snackbar.Make(message, visualOptions: snackbarOptions);
            await snackbar.Show(cancellationTokenSource.Token);
        }

        public static async Task DisplayToastAsync(string message)
        {
            if (OperatingSystem.IsWindows())
                return;

            var toast = Toast.Make(message, textSize: 18);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await toast.Show(cts.Token);
        }
    }
}
