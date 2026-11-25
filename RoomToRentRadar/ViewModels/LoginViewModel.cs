using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Storage;
using RoomToRentRadar.Validations;

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
    [NotifyPropertyChangedFor(nameof(IsStudent))]
    [NotifyPropertyChangedFor(nameof(IsLandlord))]
    public partial AppUserRole SelectedRole { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEmailError))]
    public partial string EmailError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPasswordError))]
    public partial string PasswordError { get; set; }

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
        SelectedRole = AppUserRole.Student;
        ErrorMessage = string.Empty;
        EmailError = string.Empty;
        PasswordError = string.Empty;
    }

    public bool IsNotBusy => !IsBusy;

    public bool IsStudent => SelectedRole == AppUserRole.Student;

    public bool IsLandlord => SelectedRole == AppUserRole.Landlord;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasEmailError => !string.IsNullOrEmpty(EmailError);

    public bool HasPasswordError => !string.IsNullOrEmpty(PasswordError);

    partial void OnEmailChanged(string value) => EmailError = string.Empty;

    partial void OnPasswordChanged(string value) => PasswordError = string.Empty;

    [RelayCommand]
    private void SelectStudent() => SelectedRole = AppUserRole.Student;

    [RelayCommand]
    private void SelectLandlord() => SelectedRole = AppUserRole.Landlord;

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SignInAsync(CancellationToken cancellationToken)
    {
        ClearErrors();

        var errors = RegistrationFormValidator.ValidateLogin(Email, Password);
        if (errors.Count > 0)
        {
            if (errors.TryGetValue(RegistrationFormValidator.EmailField, out var emailError))
            {
                EmailError = emailError;
            }

            if (errors.TryGetValue(RegistrationFormValidator.PasswordField, out var passwordError))
            {
                PasswordError = passwordError;
            }

            ErrorMessage = string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password)
                ? "Nhập email và mật khẩu."
                : errors.Values.First();
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
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            ErrorMessage = "Email hoặc mật khẩu chưa hợp lệ.";
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            ErrorMessage = "Bạn đăng nhập quá nhiều lần. Vui lòng thử lại sau ít phút.";
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
    private Task GoToRegisterAsync() => _navigationService.GoToRegisterAsync(SelectedRole);

    private void ClearErrors()
    {
        ErrorMessage = string.Empty;
        EmailError = string.Empty;
        PasswordError = string.Empty;
    }
}
