using System.Globalization;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
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
            LocalizationResourceManager.Instance.SetCulture(new CultureInfo("en"));
            InitializeComponent();
            _sessionService = sessionService;
            _routeGuardService = routeGuardService;
            UpdateAuthVisibility();
            Routing.RegisterRoute(AppRoutes.RoomDetail, typeof(Features.RoomDetails.Views.RoomDetailPage));
            Routing.RegisterRoute(AppRoutes.RoomDetails, typeof(RoomDetailsPage));
            Routing.RegisterRoute(AppRoutes.CreateListing, typeof(CreateListingPage));

            if (_sessionService != null)
            {
                _sessionService.CurrentUserChanged += OnCurrentUserChanged;
            }
            UpdateProfileVisibility();
        }

        public AppShell(
            HomePage homePage,
            RoomListingsPage roomListingsPage,
            AuthPage authPage,
            ProfilePage profilePage,
            IAuthenticationSessionService sessionService,
            IRouteGuardService routeGuardService)
        {
            LocalizationResourceManager.Instance.SetCulture(new CultureInfo("en"));
            InitializeComponent();
            _sessionService = sessionService;
            _routeGuardService = routeGuardService;
            UpdateAuthVisibility();

            HomeShellContent.Content = homePage;
            RoomListingsShellContent.Content = roomListingsPage;
            AuthShellContent.Content = authPage;
            ProfileShellContent.Content = profilePage;

            Routing.RegisterRoute(AppRoutes.RoomDetail, typeof(Features.RoomDetails.Views.RoomDetailPage));
            Routing.RegisterRoute(AppRoutes.RoomDetails, typeof(RoomDetailsPage));
            Routing.RegisterRoute(AppRoutes.CreateListing, typeof(CreateListingPage));

            if (_sessionService != null)
            {
                _sessionService.CurrentUserChanged += OnCurrentUserChanged;
            }
            UpdateProfileVisibility();
        }

        protected override async void OnNavigating(ShellNavigatingEventArgs args)
        {
            base.OnNavigating(args);

            if (_isRedirecting || _routeGuardService == null || _sessionService == null
                || args.Source == ShellNavigationSource.Pop || args.Source == ShellNavigationSource.PopToRoot)
            {
                return;
            }

            var targetUri = args.Target?.Location?.OriginalString;
            if (string.IsNullOrWhiteSpace(targetUri) || targetUri == "..")
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

        private void UpdateProfileVisibility() => UpdateAuthVisibility();
        private void UpdateAuthVisibility()
        {
            var isAuthenticated = _sessionService?.IsAuthenticated == true;
            if (ProfileShellContent != null)
            {
                ProfileShellContent.IsVisible = isAuthenticated;
                Shell.SetFlyoutItemIsVisible(ProfileShellContent, isAuthenticated);
            }

            if (AuthFlyoutItem != null)
            {
                AuthFlyoutItem.IsVisible = !isAuthenticated;
                Shell.SetFlyoutItemIsVisible(AuthFlyoutItem, !isAuthenticated);
            }

            if (AuthShellContent != null)
            {
                AuthShellContent.IsVisible = !isAuthenticated;
                Shell.SetFlyoutItemIsVisible(AuthShellContent, !isAuthenticated);
            }

            if (LogoutMenuItem != null)
            {
                Shell.SetFlyoutItemIsVisible(LogoutMenuItem, isAuthenticated);
            }
        }

        private async void OnLogoutMenuItemClicked(object? sender, EventArgs e)
        {
            FlyoutIsPresented = false;

            bool shouldLogout = false;
            try
            {
                var popup = new LogoutConfirmationPopup();
                var targetPage = CurrentPage ?? (Page)this;
                targetPage.ShowPopup(popup);
                shouldLogout = await popup.WaitForDecisionAsync();
            }
            catch
            {
                var confirmTitle = LocalizationResourceManager.Instance["Logout_Confirm_Title"] ?? "Log Out";
                var confirmMessage = LocalizationResourceManager.Instance["Logout_Confirm_Message"] ?? "Are you sure you want to log out?";
                var confirmYes = LocalizationResourceManager.Instance["Logout_Confirm_Yes"] ?? "Log Out";
                var confirmNo = LocalizationResourceManager.Instance["Logout_Confirm_No"] ?? "Cancel";

                var page = CurrentPage ?? this;
                shouldLogout = await page.DisplayAlertAsync(confirmTitle, confirmMessage, confirmYes, confirmNo);
            }

            if (!shouldLogout)
            {
                return;
            }

            if (_sessionService != null)
            {
                await _sessionService.LogoutAsync();
            }

            await GoToAsync($"//{AppRoutes.Home}");

            var successMsg = LocalizationResourceManager.Instance["Logout_Success"] ?? "Logged out successfully";
            await DisplayToastAsync(successMsg);
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
