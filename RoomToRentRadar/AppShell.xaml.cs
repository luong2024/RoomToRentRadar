using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Features.RoomListings.Views;
using Font = Microsoft.Maui.Font;

namespace RoomToRentRadar
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(AppRoutes.RoomDetails, typeof(Features.RoomDetails.Views.RoomDetailPage));
            Routing.RegisterRoute(AppRoutes.RoomDetail, typeof(Features.RoomDetails.Views.RoomDetailPage));
        }

        public void UpdateDesktopPaneCompactState(bool isCompact)
        {
            FlyoutHeaderExpandedPanel.IsVisible = !isCompact;
            FlyoutFooterExpandedPanel.IsVisible = !isCompact;
        }

        public static async Task DisplaySnackbarAsync(string message)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

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
            // Toast is currently not working in MCT on Windows
            if (OperatingSystem.IsWindows())
                return;

            var toast = Toast.Make(message, textSize: 18);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await toast.Show(cts.Token);
        }
    }
}
