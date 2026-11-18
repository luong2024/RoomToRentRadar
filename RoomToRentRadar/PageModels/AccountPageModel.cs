using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Utilities;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.PageModels;

public partial class AccountPageModel : ObservableObject
{
    private readonly IAuthGateway _authGateway;
    private readonly ISessionStore _sessionStore;

    [ObservableProperty]
    public partial string DisplayName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Initials { get; set; } = "ST";

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RoleBadgeText { get; set; } = "Guest · not signed in";

    [ObservableProperty]
    public partial string CampusName { get; set; } = "ICTU";

    [ObservableProperty]
    public partial string LanguageName { get; set; } = "English";

    [ObservableProperty]
    public partial int SavedSearchesCount { get; set; } = 3;

    [ObservableProperty]
    public partial string NotificationsCount { get; set; } = "2 new";

    [ObservableProperty]
    public partial bool IsPushNotificationsEnabled { get; set; } = true;

    [ObservableProperty]
    public partial bool IsBusy { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAuthenticated))]
    public partial bool IsGuest { get; set; } = true;

    public bool IsAuthenticated => !IsGuest;

    public string TitleText => AppResources.GetString("Account_Title");
    public string GuestWelcomeTitleText => AppResources.GetString("Account_Guest_WelcomeTitle");
    public string GuestWelcomeMessageText => AppResources.GetString("Account_Guest_WelcomeMessage");
    public string GuestSignInText => AppResources.GetString("Account_Guest_SignIn");
    public string GuestRegisterText => AppResources.GetString("Account_Guest_Register");
    public string ItemMyCampusText => AppResources.GetString("Account_Item_MyCampus");
    public string ItemLanguageText => AppResources.GetString("Account_Item_Language");
    public string ItemPersonalDetailsText => AppResources.GetString("Account_Item_PersonalDetails");
    public string ItemSavedSearchesText => AppResources.GetString("Account_Item_SavedSearches");
    public string ItemNotificationsText => AppResources.GetString("Account_Item_Notifications");
    public string ItemHelpText => AppResources.GetString("Account_Item_Help");
    public string ItemPushNotificationsText => AppResources.GetString("Account_Item_PushNotifications");
    public string ItemSignOutText => AppResources.GetString("Account_Item_SignOut");
    public string FooterCopyrightText => AppResources.GetString("Account_Footer_Copyright");

    public Func<string, Task>? NavigateOverride { get; set; }
    public Func<string, string, string, Task>? DisplayAlertOverride { get; set; }
    public Func<string, string, string, string[], Task<string>>? DisplayActionSheetOverride { get; set; }

    private string? _currentUserRole;
    private bool _currentUserIsVerified;
    private string? _currentUserAuthProvider;

    public AccountPageModel(
        IAuthGateway authGateway,
        ISessionStore sessionStore)
    {
        _authGateway = authGateway ?? throw new ArgumentNullException(nameof(authGateway));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));

        LocalizedStrings.Instance.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(TitleText));
            OnPropertyChanged(nameof(GuestWelcomeTitleText));
            OnPropertyChanged(nameof(GuestWelcomeMessageText));
            OnPropertyChanged(nameof(GuestSignInText));
            OnPropertyChanged(nameof(GuestRegisterText));
            OnPropertyChanged(nameof(ItemMyCampusText));
            OnPropertyChanged(nameof(ItemLanguageText));
            OnPropertyChanged(nameof(ItemPersonalDetailsText));
            OnPropertyChanged(nameof(ItemSavedSearchesText));
            OnPropertyChanged(nameof(ItemNotificationsText));
            OnPropertyChanged(nameof(ItemHelpText));
            OnPropertyChanged(nameof(ItemPushNotificationsText));
            OnPropertyChanged(nameof(ItemSignOutText));
            OnPropertyChanged(nameof(FooterCopyrightText));
        };
    }

    public async Task InitializeAsync()
    {
        // 1. Load instantly from cached profile in secure store
        var user = await _sessionStore.GetCachedUserProfileAsync();
        if (user != null)
        {
            ApplyUserProfile(user);
        }
        else
        {
            IsGuest = true;
            RoleBadgeText = "Guest · not signed in";
        }

        // 2. Refresh asynchronously from Backend API /api/auth/me if online
        try
        {
            var token = await _sessionStore.GetAccessTokenAsync();
            if (!string.IsNullOrWhiteSpace(token))
            {
                var latestProfile = await _authGateway.GetCurrentUserProfileAsync(token);
                if (latestProfile != null)
                {
                    ApplyUserProfile(latestProfile);
                }
            }
        }
        catch
        {
            // Offline or network error: gracefully keep cached profile
        }
    }

    public void ApplyUserProfile(UserProfileDto user)
    {
        _currentUserRole = user.Role;
        _currentUserIsVerified = user.IsVerified;
        _currentUserAuthProvider = user.AuthProvider;

        DisplayName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.Email;
        Email = user.Email;
        Initials = ComputeInitials(DisplayName);
        RoleBadgeText = FormatRoleBadge(user.Role, user.IsVerified, user.AuthProvider, GetCurrentLanguageCode());
        IsGuest = false;
    }

    public static string ComputeInitials(string? name) =>
        UserProfileFormatter.ComputeInitials(name);

    public static string FormatRoleBadge(string? role, bool isVerified = false, string? authProvider = null, string? language = null) =>
        UserProfileFormatter.FormatRoleBadge(role, isVerified, authProvider, language);

    private string GetCurrentLanguageCode() =>
        LanguageName.Equals("Tiếng Việt", StringComparison.OrdinalIgnoreCase) ? "vi-VN" : "en-US";

    [RelayCommand]
    private async Task SignOutAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;

            // 1. Retrieve stored tokens
            var refreshToken = await _sessionStore.GetRefreshTokenAsync();
            var accessToken = await _sessionStore.GetAccessTokenAsync();

            // 2. Connect to Backend Logout API: POST /api/auth/revoke
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                try
                {
                    var revokeRequest = new RevokeTokenRequest(refreshToken);
                    await _authGateway.RevokeTokenAsync(revokeRequest, accessToken);
                }
                catch (Exception)
                {
                    // Fail-safe: Even if server or network fails, proceed to clear local store
                    // to prevent locking the user out on this device.
                }
            }
        }
        finally
        {
            // 3. Purge device credentials & session profile
            await _sessionStore.ClearSessionAsync();
            _currentUserRole = null;
            _currentUserIsVerified = false;
            _currentUserAuthProvider = null;
            IsGuest = true;
            DisplayName = string.Empty;
            Email = string.Empty;
            Initials = "ST";
            RoleBadgeText = LanguageName.Equals("Tiếng Việt", StringComparison.OrdinalIgnoreCase)
                ? "Khách · chưa đăng nhập"
                : "Guest · not signed in";
            IsBusy = false;

            // 4. Navigate back to Login Page (hiding Bottom Navigation Bar)
            await NavigateToRouteAsync("//login");
        }
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        await NavigateToRouteAsync("//login");
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        await NavigateToRouteAsync("roleselection");
    }

    [RelayCommand]
    private async Task NavigateCampusAsync()
    {
        var title = GetLocalizedString("Account_Alert_CampusTitle", "My Campus");
        var message = GetLocalizedString("Account_Alert_CampusMessage", "ICTU (Information & Communication Technology University) is your default campus reference.");
        var ok = GetLocalizedString("Common_Ok", "OK");
        await ShowAlertAsync(title, message, ok);
    }

    [RelayCommand]
    private async Task NavigateLanguageAsync()
    {
        const string title = "Select Language / Chọn ngôn ngữ";
        const string cancel = "Cancel / Hủy";
        const string optEn = "English (en-US)";
        const string optVi = "Tiếng Việt (vi-VN)";

        string? result = null;
        if (DisplayActionSheetOverride != null)
        {
            result = await DisplayActionSheetOverride(title, cancel, null!, new[] { optEn, optVi });
        }
#if !UNIT_TESTS
        else if (Microsoft.Maui.Controls.Shell.Current != null)
        {
            result = await Microsoft.Maui.Controls.Shell.Current.DisplayActionSheetAsync(title, cancel, null, optEn, optVi);
        }
#endif

        if (string.Equals(result, optVi, StringComparison.OrdinalIgnoreCase))
        {
            SwitchLanguage("vi-VN");
        }
        else if (string.Equals(result, optEn, StringComparison.OrdinalIgnoreCase))
        {
            SwitchLanguage("en-US");
        }
    }

    public void SwitchLanguage(string cultureCode)
    {
        var culture = new System.Globalization.CultureInfo(cultureCode);
        RoomToRentRadar.Resources.Strings.LocalizedStrings.Instance.SetCulture(culture);
        LanguageName = cultureCode.StartsWith("vi", StringComparison.OrdinalIgnoreCase) ? "Tiếng Việt" : "English";

        if (IsGuest)
        {
            RoleBadgeText = cultureCode.StartsWith("vi", StringComparison.OrdinalIgnoreCase)
                ? "Khách · chưa đăng nhập"
                : "Guest · not signed in";
        }
        else
        {
            RoleBadgeText = FormatRoleBadge(_currentUserRole, _currentUserIsVerified, _currentUserAuthProvider, cultureCode);
        }
    }

    [RelayCommand]
    private async Task NavigatePersonalDetailsAsync()
    {
        var title = GetLocalizedString("Account_Alert_PersonalDetailsTitle", "Personal Details");
        var template = GetLocalizedString("Account_Alert_PersonalDetailsMessage", "Name: {0}\nEmail: {1}\nRole: {2}");
        var message = string.Format(template, DisplayName, Email, RoleBadgeText);
        var ok = GetLocalizedString("Common_Ok", "OK");
        await ShowAlertAsync(title, message, ok);
    }

    [RelayCommand]
    private async Task NavigateSavedSearchesAsync()
    {
        await NavigateToRouteAsync("//saved");
    }

    [RelayCommand]
    private async Task NavigateNotificationsAsync()
    {
        await NavigateToRouteAsync("//messages");
    }

    [RelayCommand]
    private async Task NavigateHelpAsync()
    {
        var title = GetLocalizedString("Account_Alert_HelpTitle", "Help & Privacy");
        var message = GetLocalizedString("Account_Alert_HelpMessage", "RoomToRentRadar 1.0\nGroup 2 ICTU Mobile Rental Radar Project.");
        var ok = GetLocalizedString("Common_Ok", "OK");
        await ShowAlertAsync(title, message, ok);
    }

    private static string GetLocalizedString(string key, string fallback)
    {
        try
        {
            var value = RoomToRentRadar.Resources.Strings.LocalizedStrings.Instance[key];
            return !string.IsNullOrWhiteSpace(value) && !value.StartsWith("[", StringComparison.Ordinal)
                ? value
                : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private async Task NavigateToRouteAsync(string route)
    {
        if (NavigateOverride != null)
        {
            await NavigateOverride(route);
            return;
        }

#if !UNIT_TESTS
        if (Microsoft.Maui.Controls.Shell.Current != null)
        {
            await Microsoft.Maui.Controls.Shell.Current.GoToAsync(route);
        }
#endif
    }

    private async Task ShowAlertAsync(string title, string message, string cancel)
    {
        if (DisplayAlertOverride != null)
        {
            await DisplayAlertOverride(title, message, cancel);
            return;
        }

#if !UNIT_TESTS
        if (Microsoft.Maui.Controls.Shell.Current != null)
        {
            await Microsoft.Maui.Controls.Shell.Current.DisplayAlertAsync(title, message, cancel);
        }
#endif
    }
}
