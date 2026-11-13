using System.Net;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.PageModels;

[QueryProperty(nameof(Role), "role")]
[QueryProperty(nameof(Email), "email")]
[QueryProperty(nameof(FullName), "fullName")]
[QueryProperty(nameof(GoogleToken), "googleToken")]
[QueryProperty(nameof(IsGoogleOnboardingString), "isGoogle")]
public partial class RegisterPageModel : ObservableObject
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    // BR-AUTH-03: Vietnamese phone ^(0|\+84)[0-9]{9}$
    private static readonly Regex PhoneRegex = new(@"^(0|\+84)[0-9]{9}$", RegexOptions.Compiled);

    private readonly IAuthGateway _authGateway;
    private readonly ISessionStore _sessionStore;

    [ObservableProperty]
    public partial string Role { get; set; } = "Student";

    [ObservableProperty]
    public partial bool IsStudent { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLandlord { get; set; } = false;

    [ObservableProperty]
    public partial string RoleDisplayName { get; set; } = "Student";

    [ObservableProperty]
    public partial string GoogleToken { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsGoogleOnboarding { get; set; } = false;

    public string IsGoogleOnboardingString
    {
        get => IsGoogleOnboarding.ToString();
        set => IsGoogleOnboarding = bool.TryParse(value, out var b) && b;
    }

    [ObservableProperty]
    public partial bool IsEmailEditable { get; set; } = true;

    [ObservableProperty]
    public partial bool CanSwitchRole { get; set; } = true;

    [ObservableProperty]
    public partial string FullName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StudentCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PhoneNumber { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string HouseName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string HouseAddress { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConfirmPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PageTitle { get; set; } = "Create Account";

    [ObservableProperty]
    public partial string SubmitButtonText { get; set; } = "Create Account";

    [ObservableProperty]
    public partial bool IsAgreedToTerms { get; set; } = false;

    [ObservableProperty]
    public partial string FullNameError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EmailError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StudentCodeError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PhoneNumberError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string HouseNameError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string HouseAddressError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PasswordError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConfirmPasswordError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GeneralError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasGeneralError { get; set; } = false;

    [ObservableProperty]
    public partial bool IsBusy { get; set; } = false;

    public RegisterPageModel(
        IAuthGateway authGateway,
        ISessionStore sessionStore)
    {
        _authGateway = authGateway ?? throw new ArgumentNullException(nameof(authGateway));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        PageTitle = AppResources.RegisterButtonText;
        SubmitButtonText = AppResources.RegisterButtonText;
        UpdateRoleState();
    }

    partial void OnRoleChanged(string value)
    {
        UpdateRoleState();
    }

    partial void OnIsGoogleOnboardingChanged(bool value)
    {
        if (value)
        {
            IsEmailEditable = false;
            CanSwitchRole = false;
            PageTitle = AppResources.GoogleOnboardingTitle;
            SubmitButtonText = AppResources.CompleteRegistration;
        }
        else
        {
            IsEmailEditable = true;
            CanSwitchRole = true;
            PageTitle = AppResources.RegisterButtonText;
            SubmitButtonText = AppResources.RegisterButtonText;
        }
    }

    private void UpdateRoleState()
    {
        IsStudent = !string.Equals(Role, "Landlord", StringComparison.OrdinalIgnoreCase);
        IsLandlord = !IsStudent;
        RoleDisplayName = IsStudent ? "Student" : "Landlord";
    }

    [RelayCommand]
    private void SwitchRole()
    {
        Role = IsStudent ? "Landlord" : "Student";
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (IsBusy) return;

        // Reset errors
        FullNameError = string.Empty;
        EmailError = string.Empty;
        StudentCodeError = string.Empty;
        PhoneNumberError = string.Empty;
        HouseNameError = string.Empty;
        PasswordError = string.Empty;
        ConfirmPasswordError = string.Empty;
        GeneralError = string.Empty;
        HasGeneralError = false;

        bool isValid = true;

        if (string.IsNullOrWhiteSpace(FullName) || FullName.Trim().Length < 2)
        {
            FullNameError = "Full name must be at least 2 characters.";
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            EmailError = "Please enter your email address.";
            isValid = false;
        }
        else if (!EmailRegex.IsMatch(Email.Trim()))
        {
            EmailError = "Invalid email address format.";
            isValid = false;
        }

        if (IsLandlord)
        {
            if (string.IsNullOrWhiteSpace(PhoneNumber))
            {
                PhoneNumberError = "Phone number is required for landlords.";
                isValid = false;
            }
            else if (!PhoneRegex.IsMatch(PhoneNumber.Trim()))
            {
                PhoneNumberError = "Please enter a valid Vietnamese phone number (e.g. 0912345678 or +84912345678).";
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(HouseName))
            {
                HouseNameError = "Boarding house name is required for landlords.";
                isValid = false;
            }
            else if (HouseName.Trim().Length < 3 || HouseName.Trim().Length > 100)
            {
                HouseNameError = "Boarding house name must be between 3 and 100 characters.";
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(HouseAddress))
            {
                HouseAddressError = AppResources.HouseAddressRequiredError;
                isValid = false;
            }
            else if (HouseAddress.Trim().Length < 5 || HouseAddress.Trim().Length > 250)
            {
                HouseAddressError = AppResources.HouseAddressRequiredError;
                isValid = false;
            }
        }

        // BR-AUTH-05: Password 8–128 chars with >= 1 upper, >= 1 lower, >= 1 digit
        if (string.IsNullOrWhiteSpace(Password))
        {
            PasswordError = "Password is required.";
            isValid = false;
        }
        else if (Password.Length < 8 || Password.Length > 128)
        {
            PasswordError = "Password must be between 8 and 128 characters.";
            isValid = false;
        }
        else if (!Password.Any(char.IsUpper) || !Password.Any(char.IsLower) || !Password.Any(char.IsDigit))
        {
            PasswordError = "Password must include at least one uppercase letter, one lowercase letter, and one number.";
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            ConfirmPasswordError = "Please confirm your password.";
            isValid = false;
        }
        else if (Password != ConfirmPassword)
        {
            ConfirmPasswordError = "Passwords do not match.";
            isValid = false;
        }

        if (!IsAgreedToTerms)
        {
            GeneralError = "You must agree to the Terms of Service & Privacy Policy.";
            HasGeneralError = true;
            isValid = false;
        }

        if (!isValid) return;

        try
        {
            IsBusy = true;
            var normalizedStudentCode = IsStudent && !string.IsNullOrWhiteSpace(StudentCode) ? StudentCode.Trim().ToUpperInvariant() : null;
            var normalizedPhone = !IsStudent && !string.IsNullOrWhiteSpace(PhoneNumber) ? PhoneNumber.Trim() : null;
            var normalizedHouseName = !IsStudent && !string.IsNullOrWhiteSpace(HouseName) ? HouseName.Trim() : null;
            var normalizedHouseAddress = !IsStudent && !string.IsNullOrWhiteSpace(HouseAddress) ? HouseAddress.Trim() : null;

            AuthTokensDto? response;

            if (IsGoogleOnboarding && !string.IsNullOrWhiteSpace(GoogleToken))
            {
                var googleRequest = new GoogleLoginRequest(
                    IdToken: GoogleToken,
                    Role: IsStudent ? "Student" : "Landlord",
                    PhoneNumber: normalizedPhone,
                    HouseName: normalizedHouseName,
                    HouseAddress: normalizedHouseAddress,
                    Password: Password,
                    DeviceInfo: DeviceInfo.Current.Model);

                response = await _authGateway.LoginWithGoogleAsync(googleRequest);
            }
            else
            {
                var request = new RegisterUserRequest(
                    Email: Email.Trim(),
                    Password: Password,
                    FullName: FullName.Trim(),
                    Role: IsStudent ? "Student" : "Landlord",
                    PhoneNumber: normalizedPhone,
                    StudentCode: normalizedStudentCode,
                    HouseName: normalizedHouseName,
                    HouseAddress: normalizedHouseAddress
                );

                response = await _authGateway.RegisterAsync(request);
            }

            if (response != null && !string.IsNullOrEmpty(response.AccessToken))
            {
                var saved = await _sessionStore.SaveSessionAsync(response);
                if (!saved)
                {
                    GeneralError = "Registration succeeded, but failed to securely store your session.";
                    HasGeneralError = true;
                    return;
                }

                try
                {
                    await Shell.Current.GoToAsync("//main");
                }
                catch (Exception)
                {
                    GeneralError = "Registration succeeded, but navigation to home screen failed.";
                    HasGeneralError = true;
                }
            }
            else
            {
                GeneralError = "Registration failed. Please check your details.";
                HasGeneralError = true;
            }
        }
        catch (ApiException apiEx)
        {
            GeneralError = MapRegisterErrorCodeToMessage(apiEx.ErrorCode, apiEx.StatusCode, apiEx.Message);
            HasGeneralError = true;
        }
        catch (HttpRequestException)
        {
            GeneralError = "Unable to connect to the server. Please check your internet connection.";
            HasGeneralError = true;
        }
        catch (Exception)
        {
            GeneralError = "An unexpected error occurred during registration. Please try again.";
            HasGeneralError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task NavigateToLoginAsync()
    {
        await Shell.Current.GoToAsync("//login");
    }

    private static string MapRegisterErrorCodeToMessage(string? errorCode, HttpStatusCode statusCode, string defaultMessage)
    {
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            return errorCode switch
            {
                "EMAIL_TAKEN" or "USER_EMAIL_EXISTS" or "AUTH_EMAIL_EXISTS"
                    => AppResources.EmailAlreadyInUse,
                "INVALID_STUDENT_CODE" => AppResources.InvalidStudentCode,
                "INVALID_PHONE_NUMBER" or "AUTH_PHONE_REQUIRED" => AppResources.PhoneNumberRequiredMessage,
                "AUTH_HOUSE_NAME_REQUIRED" or "INVALID_HOUSE_NAME" => AppResources.HouseNameRequiredMessage,
                "AUTH_HOUSE_ADDRESS_REQUIRED" or "INVALID_HOUSE_ADDRESS" => AppResources.HouseAddressRequiredMessage,
                "INVALID_ROLE" => AppResources.InvalidRoleSelected,
                _ => !string.IsNullOrWhiteSpace(defaultMessage) ? defaultMessage : AppResources.RegistrationFailedDefault
            };
        }

        return statusCode switch
        {
            HttpStatusCode.Conflict => AppResources.EmailAlreadyInUse,
            HttpStatusCode.BadRequest => AppResources.RegistrationCheckInfo,
            _ => !string.IsNullOrWhiteSpace(defaultMessage) ? defaultMessage : AppResources.RegistrationFailedDefault
        };
    }
}
