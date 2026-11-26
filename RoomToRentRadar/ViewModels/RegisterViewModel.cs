using System.Net;
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

public partial class RegisterViewModel : ObservableObject, IQueryAttributable
{
    private readonly AuthService _authService;
    private readonly IErrorHandler _errorHandler;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    public partial string DisplayName { get; set; }

    [ObservableProperty]
    public partial string Email { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMinPasswordLength))]
    [NotifyPropertyChangedFor(nameof(HasPasswordLetter))]
    [NotifyPropertyChangedFor(nameof(HasPasswordDigit))]
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDisplayNameError))]
    public partial string DisplayNameError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEmailError))]
    public partial string EmailError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPasswordError))]
    public partial string PasswordError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasConfirmPasswordError))]
    public partial string ConfirmPasswordError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRoleError))]
    public partial string RoleError { get; set; }

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
        DisplayNameError = string.Empty;
        EmailError = string.Empty;
        PasswordError = string.Empty;
        ConfirmPasswordError = string.Empty;
        RoleError = string.Empty;
    }

    public bool IsNotBusy => !IsBusy;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasDisplayNameError => !string.IsNullOrEmpty(DisplayNameError);

    public bool HasEmailError => !string.IsNullOrEmpty(EmailError);

    public bool HasPasswordError => !string.IsNullOrEmpty(PasswordError);

    public bool HasConfirmPasswordError => !string.IsNullOrEmpty(ConfirmPasswordError);

    public bool HasRoleError => !string.IsNullOrEmpty(RoleError);

    public bool HasMinPasswordLength => RegistrationFormValidator.HasMinimumPasswordLength(Password);

    public bool HasPasswordLetter => RegistrationFormValidator.HasPasswordLetter(Password);

    public bool HasPasswordDigit => RegistrationFormValidator.HasPasswordDigit(Password);

    public bool IsStudent => SelectedRole == AppUserRole.Student;

    public bool IsLandlord => SelectedRole == AppUserRole.Landlord;

    partial void OnDisplayNameChanged(string value) => DisplayNameError = string.Empty;

    partial void OnEmailChanged(string value) => EmailError = string.Empty;

    partial void OnPasswordChanged(string value) => PasswordError = string.Empty;

    partial void OnConfirmPasswordChanged(string value) => ConfirmPasswordError = string.Empty;

    partial void OnSelectedRoleChanged(AppUserRole value) => RoleError = string.Empty;

    [RelayCommand]
    private void SelectStudent() => SelectedRole = AppUserRole.Student;

    [RelayCommand]
    private void SelectLandlord() => SelectedRole = AppUserRole.Landlord;

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RegisterAsync(CancellationToken cancellationToken)
    {
        ClearErrors();

        var errors = RegistrationFormValidator.ValidateFields(
            DisplayName,
            Email,
            Password,
            ConfirmPassword,
            SelectedRole);

        if (errors.Count > 0)
        {
            ApplyFieldErrors(errors);
            ErrorMessage = errors.Values.First();
            return;
        }

        IsBusy = true;
        try
        {
            var challenge = await _authService.BeginRegistrationAsync(
                new RegisterRequest(
                    Email.Trim(),
                    Password,
                    DisplayName.Trim(),
                    AppUserRoleWire.ToWireValue(SelectedRole)),
                cancellationToken);

            Password = string.Empty;
            ConfirmPassword = string.Empty;
            await _navigationService.GoToOtpVerificationAsync(challenge.ChallengeId, challenge.Email);
        }
        catch (ApiException ex) when (ex.Code == "email_already_registered")
        {
            const string duplicateMessage = "Email này đã được đăng ký. Hãy đăng nhập hoặc dùng email khác.";
            EmailError = duplicateMessage;
            ErrorMessage = duplicateMessage;
        }
        catch (ApiException ex) when (ex.Code == "email_delivery_failed")
        {
            ErrorMessage = "Không gửi được email xác nhận. Kiểm tra địa chỉ email và thử lại.";
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            ErrorMessage = "Bạn thao tác quá nhanh. Vui lòng thử lại sau ít phút.";
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            ApplyServerValidationCode(ex.Code);
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

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("role", out var rawRole)
            && rawRole is string roleWire
            && AppUserRoleWire.TryParse(roleWire) is AppUserRole.Student or AppUserRole.Landlord)
        {
            SelectedRole = AppUserRoleWire.TryParse(roleWire)!.Value;
        }
    }

    private void ClearErrors()
    {
        ErrorMessage = string.Empty;
        DisplayNameError = string.Empty;
        EmailError = string.Empty;
        PasswordError = string.Empty;
        ConfirmPasswordError = string.Empty;
        RoleError = string.Empty;
    }

    private void ApplyFieldErrors(IReadOnlyDictionary<string, string> errors)
    {
        if (errors.TryGetValue(RegistrationFormValidator.DisplayNameField, out var displayNameError))
        {
            DisplayNameError = displayNameError;
        }

        if (errors.TryGetValue(RegistrationFormValidator.EmailField, out var emailError))
        {
            EmailError = emailError;
        }

        if (errors.TryGetValue(RegistrationFormValidator.PasswordField, out var passwordError))
        {
            PasswordError = passwordError;
        }

        if (errors.TryGetValue(RegistrationFormValidator.ConfirmPasswordField, out var confirmPasswordError))
        {
            ConfirmPasswordError = confirmPasswordError;
        }

        if (errors.TryGetValue(RegistrationFormValidator.RoleField, out var roleError))
        {
            RoleError = roleError;
        }
    }

    private void ApplyServerValidationCode(string? code)
    {
        switch (code)
        {
            case "invalid_display_name":
                DisplayNameError = $"Nhập họ tên (tối đa {AccountRules.DisplayNameMaxLength} ký tự).";
                break;
            case "invalid_email":
                EmailError = "Email không hợp lệ.";
                break;
            case "weak_password":
                PasswordError = $"Mật khẩu cần {AccountRules.PasswordMinLength}-{AccountRules.PasswordMaxLength} ký tự, gồm cả chữ và số.";
                break;
            case "invalid_role":
                RoleError = "Chọn vai trò Sinh viên hoặc Chủ phòng.";
                break;
        }
    }
}
