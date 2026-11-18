using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using RoomToRentRadar.Client.Adapters.Abstractions;
using Font = Microsoft.Maui.Font;

namespace RoomToRentRadar
{
    public partial class AppShell : Shell
    {
        private readonly ISessionStore? _sessionStore;

        public AppShell(ISessionStore? sessionStore = null)
        {
            _sessionStore = sessionStore;
            InitializeComponent();

            // Register modal/push routes
            Routing.RegisterRoute("register", typeof(Pages.RegisterPage));
            Routing.RegisterRoute("roleselection", typeof(Pages.RoleSelectionPage));

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

        private async Task CheckInitialSessionAsync()
        {
            if (_sessionStore == null) return;

            try
            {
                // App starts at //main (Explore) by default to support Guest Browsing (docs/ui-ux-spec.md §1 Principle 7).
                // If the user already has a valid token, we stay on //main.
                // If on login route with a valid token, redirect to //main.
                bool hasValid = await _sessionStore.HasValidTokenAsync();
                if (hasValid && CurrentState?.Location?.ToString()?.Contains("login") == true)
                {
                    await GoToAsync("//main");
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
