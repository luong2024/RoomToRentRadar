using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Storage;

namespace RoomToRentRadar.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _authService;
    private readonly IErrorHandler _errorHandler;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    public partial string Email { get; set; }

    [ObservableProperty]
    public partial string Password { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string ErrorMessage { get; set; }

    public LoginViewModel(
        AuthService authService,
        IErrorHandler errorHandler,
        INavigationService navigationService)
    {
        _authService = authService;
        _errorHandler = errorHandler;
        _navigationService = navigationService;
        Email = string.Empty;
        Password = string.Empty;
        ErrorMessage = string.Empty;
    }

    public bool IsNotBusy => !IsBusy;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SignInAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Nhập email và mật khẩu.";
            return;
        }

        IsBusy = true;
        try
        {
            await _authService.SignInAsync(
                Email.Trim(),
                Password,
                cancellationToken);

            Password = string.Empty;
            await _navigationService.GoToRoleRootAsync(_authService.CurrentRole);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            ErrorMessage = "Email hoặc mật khẩu không đúng.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Không thể đăng nhập. Kiểm tra kết nối và thử lại.";
            _errorHandler.HandleError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task CancelAsync() => _navigationService.GoBackAsync();

    [RelayCommand]
    private Task GoToRegisterAsync() => _navigationService.GoToRegisterAsync();
}
