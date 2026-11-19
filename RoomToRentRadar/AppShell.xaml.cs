using System.Globalization;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Fonts;
using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Localization;
using RoomToRentRadar.Core.Models.Authentication;
using RoomToRentRadar.Features.Authentication.Views;
using RoomToRentRadar.Features.Home.Views;
using RoomToRentRadar.Features.Profile.Views;
using RoomToRentRadar.Features.RoomListings.Views;
using RoomToRentRadar.Infrastructure.Services;
using Font = Microsoft.Maui.Font;

namespace RoomToRentRadar
{
    public partial class AppShell : Shell
    {
        private IAuthenticationSessionService? _sessionService;
        private readonly IRouteGuardService? _routeGuardService;
        private bool _isRedirecting;

        public AppShell() : this(null, null)
        {
        }

        public AppShell(
            IAuthenticationSessionService? sessionService,
            IRouteGuardService? routeGuardService)
        {
            InitializeComponent();
            _sessionService = sessionService;
            _routeGuardService = routeGuardService;
            Routing.RegisterRoute(AppRoutes.RoomDetail, typeof(Features.RoomDetails.Views.RoomDetailPage));
            Routing.RegisterRoute(AppRoutes.RoomDetails, typeof(RoomDetailsPage));
            Routing.RegisterRoute(AppRoutes.CreateListing, typeof(CreateListingPage));

            UpdateLanguageButtonStyles();
            LocalizationResourceManager.Instance.PropertyChanged += (_, _) => UpdateLanguageButtonStyles();

            if (_sessionService != null)
            {
                _sessionService.CurrentUserChanged += OnCurrentUserChanged;
            }
        }

        public AppShell(
            HomePage homePage,
            RoomListingsPage roomListingsPage,
            AuthPage authPage,
            ProfilePage profilePage,
            CreateListingPage createListingPage,
            IAuthenticationSessionService sessionService,
            IRouteGuardService routeGuardService)
        {
            InitializeComponent();
            _sessionService = sessionService;
            _routeGuardService = routeGuardService;

            HomeShellContent.Content = homePage;
            RoomListingsShellContent.Content = roomListingsPage;
            AuthShellContent.Content = authPage;
            ProfileShellContent.Content = profilePage;
            CreateListingShellContent.Content = createListingPage;

            Routing.RegisterRoute(AppRoutes.RoomDetail, typeof(Features.RoomDetails.Views.RoomDetailPage));
            Routing.RegisterRoute(AppRoutes.RoomDetails, typeof(RoomDetailsPage));
            Routing.RegisterRoute(AppRoutes.CreateListing, typeof(CreateListingPage));

            UpdateLanguageButtonStyles();
            LocalizationResourceManager.Instance.PropertyChanged += (_, _) => UpdateLanguageButtonStyles();

            if (_sessionService != null)
            {
                _sessionService.CurrentUserChanged += OnCurrentUserChanged;
            }
        }

        protected override async void OnNavigating(ShellNavigatingEventArgs args)
        {
            base.OnNavigating(args);

            if (_isRedirecting || _routeGuardService == null || _sessionService == null)
            {
                return;
            }

            var targetUri = args.Target?.Location?.OriginalString;
            if (string.IsNullOrWhiteSpace(targetUri))
            {
                return;
            }

            var deferral = args.GetDeferral();
            RouteGuardDecision decision;
            try
            {
                decision = await _routeGuardService.InterceptNavigationAsync(targetUri);
            }
            catch
            {
                // Fail-closed: cancel navigation and redirect to a safe fallback route when guard check throws
                decision = new RouteGuardDecision(
                    RouteAccessStatus.RequiresAuthentication,
                    IsAllowed: false,
                    RedirectRoute: $"//{AppRoutes.Home}");
            }

            if (decision.IsAllowed)
            {
                deferral.Complete();
                return;
            }

            args.Cancel();
            deferral.Complete();

            var redirectRoute = decision.RedirectRoute ?? $"//{AppRoutes.Home}";
            var alertMessage = decision.AlertMessage;
            var isRoleMismatch = decision.Status == RouteAccessStatus.AccessDeniedRoleMismatch;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (_isRedirecting)
                {
                    return;
                }

                try
                {
                    _isRedirecting = true;
                    if (Current != null)
                    {
                        await Current.GoToAsync(redirectRoute);
                    }

                    if (isRoleMismatch && !string.IsNullOrWhiteSpace(alertMessage))
                    {
                        if (Current?.CurrentPage != null)
                        {
                            await Current.CurrentPage.DisplayAlertAsync("Access Denied", alertMessage, "OK");
                        }
                        else
                        {
                            await DisplaySnackbarAsync(alertMessage);
                        }
                    }
                }
                catch
                {
                    // Suppress navigation transition race errors
                }
                finally
                {
                    _isRedirecting = false;
                }
            });
        }

        private void OnCurrentUserChanged(object? sender, AuthUser? user)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                UpdateProfileVisibility();

                if (user != null || _routeGuardService == null)
                {
                    return;
                }

                if (_isRedirecting || Current?.CurrentState?.Location == null)
                {
                    return;
                }

                var currentUri = Current.CurrentState.Location.OriginalString;
                var accessStatus = _routeGuardService.EvaluateRouteAccess(currentUri, null);
                if (accessStatus == RouteAccessStatus.RequiresAuthentication)
                {
                    try
                    {
                        _isRedirecting = true;
                        var loginRedirect = RouteGuardService.BuildLoginRedirectRoute(currentUri);
                        await Current.GoToAsync(loginRedirect);
                    }
                    catch
                    {
                        // Suppress navigation exceptions on background session teardown
                    }
                    finally
                    {
                        _isRedirecting = false;
                    }
                }
            });
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

        public void ConfigureSession(IAuthenticationSessionService sessionService)
        {
            if (ReferenceEquals(_sessionService, sessionService)) return;

            if (_sessionService is not null)
            {
                _sessionService.CurrentUserChanged -= OnCurrentUserChanged;
            }

            _sessionService = sessionService;
            _sessionService.CurrentUserChanged += OnCurrentUserChanged;
            UpdateProfileVisibility();
        }

        private void UpdateProfileVisibility() =>
            ProfileShellContent.IsVisible = _sessionService?.IsAuthenticated == true;

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
