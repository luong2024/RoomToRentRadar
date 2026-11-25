using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Profile;

namespace RoomToRentRadar.ViewModels;

public partial class AccountSettingsViewModel : ObservableObject
{
    private readonly IProfileClient _client;
    private readonly AuthService _authService;
    private readonly INavigationService _navigationService;
    private readonly IAlertPrompt _alertPrompt;
    private readonly IErrorHandler _errorHandler;
    private readonly IProfileDetailsStore _detailsStore;

    private Guid _userId;
    private bool _isLoaded;
    private bool _isApplyingSettings;

    public AccountSettingsViewModel(
        IProfileClient client,
        AuthService authService,
        INavigationService navigationService,
        IAlertPrompt alertPrompt,
        IErrorHandler errorHandler,
        IProfileDetailsStore detailsStore)
    {
        _client = client;
        _authService = authService;
        _navigationService = navigationService;
        _alertPrompt = alertPrompt;
        _errorHandler = errorHandler;
        _detailsStore = detailsStore;
        State = ProfileState.Loading;
        Email = string.Empty;
        Message = string.Empty;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadingState))]
    [NotifyPropertyChangedFor(nameof(IsContentState))]
    [NotifyPropertyChangedFor(nameof(IsSignedOutState))]
    [NotifyPropertyChangedFor(nameof(IsErrorState))]
    public partial ProfileState State { get; set; }

    [ObservableProperty]
    public partial string Email { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string Message { get; set; }

    [ObservableProperty]
    public partial bool NotificationsEnabled { get; set; }

    [ObservableProperty]
    public partial bool NewListingsEnabled { get; set; }

    [ObservableProperty]
    public partial bool ShowPhoneEnabled { get; set; }

    [ObservableProperty]
    public partial bool AllowMessagesEnabled { get; set; }

    public bool IsLoadingState => State == ProfileState.Loading;

    public bool IsContentState => State == ProfileState.Content;

    public bool IsSignedOutState => State == ProfileState.SignedOut;

    public bool IsErrorState => State == ProfileState.Error;

    public bool HasMessage => !string.IsNullOrEmpty(Message);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        State = ProfileState.Loading;
        Message = string.Empty;
        _isLoaded = false;
        try
        {
            if (!await _authService.IsSignedInAsync(cancellationToken))
            {
                State = ProfileState.SignedOut;
                return;
            }

            var profile = await _client.GetProfileAsync(cancellationToken);
            _userId = profile.UserId;
            Email = profile.Email;
            var settings = _detailsStore.GetSettings(_userId);
            _isApplyingSettings = true;
            NotificationsEnabled = settings.NotificationsEnabled;
            NewListingsEnabled = settings.NewListingsEnabled;
            ShowPhoneEnabled = settings.ShowPhoneEnabled;
            AllowMessagesEnabled = settings.AllowMessagesEnabled;
            _isApplyingSettings = false;
            _isLoaded = true;
            State = ProfileState.Content;
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            State = ProfileState.SignedOut;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            State = ProfileState.Error;
            Message = "Không thể tải cài đặt. Kiểm tra kết nối và thử lại.";
            _errorHandler.HandleError(ex);
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SignOutAsync(CancellationToken cancellationToken)
    {
        var confirmed = await _alertPrompt.ConfirmAsync(
            "Đăng xuất",
            "Bạn có chắc muốn đăng xuất khỏi tài khoản này?",
            "Đăng xuất",
            "Hủy",
            cancellationToken);
        if (!confirmed)
        {
            return;
        }

        await _authService.SignOutAsync(cancellationToken);
        await _navigationService.GoToRoleRootAsync(null);
    }

    [RelayCommand]
    private Task ChangePasswordAsync()
        => ShowPlaceholderAsync("Đổi mật khẩu", "Đổi mật khẩu sẽ có trong bản cập nhật tới.");

    [RelayCommand]
    private Task AccountSecurityAsync()
        => ShowPlaceholderAsync("Bảo mật tài khoản", "Cài đặt bảo mật nâng cao sẽ có trong bản cập nhật tới.");

    [RelayCommand]
    private Task HelpCenterAsync()
        => ShowPlaceholderAsync("Trung tâm trợ giúp", "Trung tâm trợ giúp sẽ có trong bản cập nhật tới.");

    [RelayCommand]
    private Task ReportProblemAsync()
        => ShowPlaceholderAsync("Báo cáo sự cố", "Gửi báo cáo sự cố sẽ có trong bản cập nhật tới.");

    [RelayCommand]
    private Task DeleteAccountAsync()
        => _alertPrompt.ShowAsync(
            "Chưa hỗ trợ",
            "Xóa tài khoản chưa được hỗ trợ trong bản demo. Tài khoản của bạn vẫn được giữ nguyên.",
            "Đã hiểu");

    [RelayCommand]
    private Task GoBackAsync() => _navigationService.GoBackAsync();

    [RelayCommand]
    private Task SignInAsync() => _navigationService.GoToLoginAsync();

    partial void OnNotificationsEnabledChanged(bool value) => PersistSettings();

    partial void OnNewListingsEnabledChanged(bool value) => PersistSettings();

    partial void OnShowPhoneEnabledChanged(bool value) => PersistSettings();

    partial void OnAllowMessagesEnabledChanged(bool value) => PersistSettings();

    private void PersistSettings()
    {
        if (!_isLoaded || _isApplyingSettings)
        {
            return;
        }

        _detailsStore.SaveSettings(_userId, new UserSettings
        {
            NotificationsEnabled = NotificationsEnabled,
            NewListingsEnabled = NewListingsEnabled,
            ShowPhoneEnabled = ShowPhoneEnabled,
            AllowMessagesEnabled = AllowMessagesEnabled
        });
    }

    private Task ShowPlaceholderAsync(string title, string message)
        => _alertPrompt.ShowAsync(title, message, "Đã hiểu");
}
