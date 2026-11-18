using System.Globalization;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Fonts;
using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Features.Authentication.Views;
using RoomToRentRadar.Features.Home.Views;
using RoomToRentRadar.Core.Localization;
using RoomToRentRadar.Features.RoomListings.Views;
using Font = Microsoft.Maui.Font;

namespace RoomToRentRadar
{
    public partial class AppShell : Shell
    {
        public AppShell(HomePage homePage, RoomListingsPage roomListingsPage, AuthPage authPage)
        {
            InitializeComponent();
            HomeShellContent.Content = homePage;
            RoomListingsShellContent.Content = roomListingsPage;
            AuthShellContent.Content = authPage;
            Routing.RegisterRoute(AppRoutes.RoomDetail, typeof(Features.RoomDetails.Views.RoomDetailPage));

            UpdateLanguageButtonStyles();
            LocalizationResourceManager.Instance.PropertyChanged += (_, _) => UpdateLanguageButtonStyles();
        }

        private void OnToggleLanguageMenuTapped(object? sender, EventArgs e)
        {
            LanguageOptionsPanel.IsVisible = !LanguageOptionsPanel.IsVisible;
            LanguageDropdownChevron.Text = LanguageOptionsPanel.IsVisible
                ? FluentUI.chevron_up_16_regular
                : FluentUI.chevron_down_16_regular;
        }

        private void OnSelectVietnameseTapped(object? sender, EventArgs e)
        {
            LocalizationResourceManager.Instance.SetCulture(new CultureInfo("vi"));
            LanguageOptionsPanel.IsVisible = false;
            LanguageDropdownChevron.Text = FluentUI.chevron_down_16_regular;
            UpdateLanguageButtonStyles();
        }

        private void OnSelectEnglishTapped(object? sender, EventArgs e)
        {
            LocalizationResourceManager.Instance.SetCulture(new CultureInfo("en"));
            LanguageOptionsPanel.IsVisible = false;
            LanguageDropdownChevron.Text = FluentUI.chevron_down_16_regular;
            UpdateLanguageButtonStyles();
        }

        private void UpdateLanguageButtonStyles()
        {
            var isVi = LocalizationResourceManager.Instance.IsVietnamese;

            var primaryBrand = Application.Current?.Resources.TryGetValue("PrimaryBrand", out var pColor) == true && pColor is Color p
                ? p
                : Color.FromArgb("#2563EB");

            var primaryBrandSoft = Application.Current?.Resources.TryGetValue("PrimaryBrandSoft", out var psColor) == true && psColor is Color ps
                ? ps
                : Color.FromArgb("#EFF6FF");

            var textPrimary = Application.Current?.Resources.TryGetValue("TextPrimarySlate", out var tpColor) == true && tpColor is Color tp
                ? tp
                : Color.FromArgb("#0F172A");

            SelectedLanguageBadgeLabel.Text = isVi ? "🇻🇳 Tiếng Việt" : "🇬🇧 English";

            if (isVi)
            {
                VietnameseButton.BackgroundColor = primaryBrandSoft;
                VietnameseLabel.TextColor = primaryBrand;
                VietnameseLabel.FontAttributes = FontAttributes.Bold;
                VietnameseCheckmark.IsVisible = true;
                VietnameseCheckmark.TextColor = primaryBrand;

                EnglishButton.BackgroundColor = Colors.Transparent;
                EnglishLabel.TextColor = textPrimary;
                EnglishLabel.FontAttributes = FontAttributes.None;
                EnglishCheckmark.IsVisible = false;
            }
            else
            {
                EnglishButton.BackgroundColor = primaryBrandSoft;
                EnglishLabel.TextColor = primaryBrand;
                EnglishLabel.FontAttributes = FontAttributes.Bold;
                EnglishCheckmark.IsVisible = true;
                EnglishCheckmark.TextColor = primaryBrand;

                VietnameseButton.BackgroundColor = Colors.Transparent;
                VietnameseLabel.TextColor = textPrimary;
                VietnameseLabel.FontAttributes = FontAttributes.None;
                VietnameseCheckmark.IsVisible = false;
            }
        }

        public void UpdateDesktopPaneCompactState(bool isCompact)
        {
            FlyoutHeaderExpandedPanel.IsVisible = !isCompact;
            FlyoutFooterExpandedPanel.IsVisible = !isCompact;
            if (isCompact)
            {
                LanguageOptionsPanel.IsVisible = false;
            }
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
