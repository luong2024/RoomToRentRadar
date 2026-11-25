using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Validations;

namespace RoomToRentRadar.ViewModels;

public partial class RegisterViewModel : ObservableObject
{
    private readonly AuthService _authService;
    private readonly IErrorHandler _errorHandler;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    public partial string DisplayName { get; set; }

    [ObservableProperty]
    public partial string Email { get; set; }

    [ObservableProperty]
    public partial string Password { get; set; }

    [ObservableProperty]
    public partial string ConfirmPassword { get; set; }

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

    public RegisterViewModel(
        AuthService authService,
        IErrorHandler errorHandler,
        INavigationService navigationService)
    {
        _authService = authService;
        _errorHandler = errorHandler;
        _navigationService = navigationService;
        DisplayName = string.Empty;
        Email = string.Empty;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        SelectedRole = AppUserRole.Student;
        ErrorMessage = string.Empty;
    }

    public bool IsNotBusy => !IsBusy;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsStudent => SelectedRole == AppUserRole.Student;

    public bool IsLandlord => SelectedRole == AppUserRole.Landlord;

    [RelayCommand]
    private void SelectStudent() => SelectedRole = AppUserRole.Student;

    [RelayCommand]
    private void SelectLandlord() => SelectedRole = AppUserRole.Landlord;

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RegisterAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = string.Empty;

        if (!RegistrationFormValidator.TryValidate(
                DisplayName,
                Email,
                Password,
                ConfirmPassword,
                SelectedRole,
                out var error))
        {
            ErrorMessage = error!;
            return;
        }

        IsBusy = true;
        try
        {
            await _authService.RegisterAsync(
                new RegisterRequest(
                    Email.Trim(),
                    Password,
                    DisplayName.Trim(),
                    AppUserRoleWire.ToWireValue(SelectedRole)),
                cancellationToken);

            Password = string.Empty;
            ConfirmPassword = string.Empty;
            await _navigationService.GoToRoleRootAsync(_authService.CurrentRole);
        }
        catch (ApiException ex) when (ex.Code == "email_already_registered")
        {
            ErrorMessage = "Email này đã được đăng ký. Hãy đăng nhập hoặc dùng email khác.";
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            ErrorMessage = "Thông tin đăng ký chưa hợp lệ. Kiểm tra lại họ tên, email và mật khẩu.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Không thể đăng ký. Kiểm tra kết nối và thử lại.";
            _errorHandler.HandleError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task BackAsync() => _navigationService.GoBackAsync();
}
