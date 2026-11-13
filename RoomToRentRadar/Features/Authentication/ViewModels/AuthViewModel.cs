using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Models.Authentication;

namespace RoomToRentRadar.Features.Authentication.ViewModels;

public partial class AuthViewModel : ObservableObject
{
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IAuthenticationPort _authenticationPort;
    private readonly IGoogleAuthAdapter _googleAuthAdapter;
    private readonly IAuthenticationSessionService _sessionService;

    public AuthViewModel(
        IAuthenticationPort authenticationPort,
        IGoogleAuthAdapter googleAuthAdapter,
        IAuthenticationSessionService sessionService)
    {
        _authenticationPort = authenticationPort;
        _googleAuthAdapter = googleAuthAdapter;
        _sessionService = sessionService;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoginView))]
    public partial bool IsSignUpView { get; set; }

    public bool IsLoginView => !IsSignUpView;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsSuccess { get; set; }

    [ObservableProperty]
    public partial string? SuccessMessage { get; set; }

    // Login fields
    [ObservableProperty]
    public partial string LoginEmail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LoginPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsPasswordHidden { get; set; } = true;

    [ObservableProperty]
    public partial bool IsConfirmPasswordHidden { get; set; } = true;

    [ObservableProperty]
    public partial string? LoginEmailError { get; set; }

    [ObservableProperty]
    public partial string? LoginPasswordError { get; set; }

    // Sign Up fields
    [ObservableProperty]
    public partial string SignUpEmail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SignUpPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SignUpConfirmPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? SignUpEmailError { get; set; }

    [ObservableProperty]
    public partial string? SignUpPasswordError { get; set; }

    [ObservableProperty]
    public partial string? SignUpConfirmPasswordError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStudentSelected))]
    [NotifyPropertyChangedFor(nameof(IsLandlordSelected))]
    public partial string? SelectedRole { get; set; }

    public bool IsStudentSelected => SelectedRole == UserRoleConstants.Student;
    public bool IsLandlordSelected => SelectedRole == UserRoleConstants.Landlord;

    [ObservableProperty]
    public partial string? RoleError { get; set; }

    // Google Role Selection Dialog fields
    [ObservableProperty]
    public partial bool IsRoleSelectionDialogOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGoogleStudentSelected))]
    [NotifyPropertyChangedFor(nameof(IsGoogleLandlordSelected))]
    public partial string? GoogleSelectedRole { get; set; }

    public bool IsGoogleStudentSelected => GoogleSelectedRole == UserRoleConstants.Student;
    public bool IsGoogleLandlordSelected => GoogleSelectedRole == UserRoleConstants.Landlord;

    [ObservableProperty]
    public partial string? GoogleRoleError { get; set; }

    [ObservableProperty]
    public partial string? PendingGoogleIdToken { get; set; }

    public Func<Task>? NavigateToHomeHandler { get; set; }

    public Func<Task>? FlipToLoginHandler { get; set; }

    public Func<AuthUser, Task>? OnAuthenticationSuccessHandler { get; set; }

    [RelayCommand]
    public void ToggleForm()
    {
        ClearErrors();
        IsSignUpView = !IsSignUpView;
    }

    [RelayCommand]
    public void TogglePasswordVisibility()
    {
        IsPasswordHidden = !IsPasswordHidden;
    }

    [RelayCommand]
    public void ToggleConfirmPasswordVisibility()
    {
        IsConfirmPasswordHidden = !IsConfirmPasswordHidden;
    }

    [RelayCommand]
    public async Task SkipAuthAsync()
    {
        if (NavigateToHomeHandler != null)
        {
            await NavigateToHomeHandler.Invoke();
        }
    }

    [RelayCommand]
    public void SelectRole(string? role)
    {
        if (role is UserRoleConstants.Student or UserRoleConstants.Landlord)
        {
            SelectedRole = role;
            RoleError = null;
        }
    }

    [RelayCommand]
    public void SelectGoogleRole(string? role)
    {
        if (role is UserRoleConstants.Student or UserRoleConstants.Landlord)
        {
            GoogleSelectedRole = role;
            GoogleRoleError = null;
        }
    }

    [RelayCommand]
    public void CancelGoogleRole()
    {
        IsRoleSelectionDialogOpen = false;
        GoogleSelectedRole = null;
        GoogleRoleError = null;
        PendingGoogleIdToken = null;
    }

    [RelayCommand]
    public async Task SignInAsync(CancellationToken cancellationToken)
    {
        if (IsLoading) return;

        ClearStatus();

        if (!ValidateLoginForm())
        {
            return;
        }

        try
        {
            IsLoading = true;
            var normalizedEmail = LoginEmail.Trim().ToLowerInvariant();
            var request = new LoginWithEmailRequest(normalizedEmail, LoginPassword);

            var result = await _authenticationPort.LoginWithEmailAsync(request, cancellationToken);
            if (result.IsSuccess)
            {
                SetSuccess("Signed in successfully!");

                if (!string.IsNullOrWhiteSpace(result.AccessToken) && result.User != null)
                {
                    await _sessionService.SaveSessionAsync(result.AccessToken, result.RefreshToken ?? string.Empty, result.User, cancellationToken);
                }

                if (OnAuthenticationSuccessHandler != null && result.User != null)
                {
                    await OnAuthenticationSuccessHandler.Invoke(result.User);
                }
            }
            else
            {
                SetError(result.ErrorMessage ?? "Sign in failed. Please check your credentials.");
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SignUpAsync(CancellationToken cancellationToken)
    {
        if (IsLoading) return;

        ClearStatus();

        if (!ValidateSignUpForm())
        {
            return;
        }

        try
        {
            IsLoading = true;
            var normalizedEmail = SignUpEmail.Trim().ToLowerInvariant();
            var request = new RegisterUserRequest(normalizedEmail, SignUpPassword, SelectedRole!);

            var result = await _authenticationPort.RegisterAsync(request, cancellationToken);
            if (result.IsSuccess)
            {
                SetSuccess("Account created successfully! You can now sign in.");
                if (FlipToLoginHandler != null)
                {
                    await FlipToLoginHandler.Invoke();
                }
            }
            else
            {
                SetError(result.ErrorMessage ?? "Account creation failed.");
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task GoogleSignInAsync(CancellationToken cancellationToken)
    {
        if (IsLoading) return;

        ClearStatus();

        try
        {
            IsLoading = true;
            var googleAuthResult = await _googleAuthAdapter.AuthenticateWithGoogleAsync(cancellationToken);

            if (!googleAuthResult.IsSuccess || string.IsNullOrWhiteSpace(googleAuthResult.IdToken))
            {
                SetError(googleAuthResult.ErrorMessage ?? "Google sign-in was canceled or failed. Please try again.");
                return;
            }

            var loginRequest = new LoginWithGoogleRequest(googleAuthResult.IdToken);
            var result = await _authenticationPort.LoginWithGoogleAsync(loginRequest, cancellationToken);

            if (result.IsSuccess)
            {
                SetSuccess("Google sign-in successful!");

                if (!string.IsNullOrWhiteSpace(result.AccessToken) && result.User != null)
                {
                    await _sessionService.SaveSessionAsync(result.AccessToken, result.RefreshToken ?? string.Empty, result.User, cancellationToken);
                }

                if (OnAuthenticationSuccessHandler != null && result.User != null)
                {
                    await OnAuthenticationSuccessHandler.Invoke(result.User);
                }
            }
            else if (result.RequiresRoleSelection || !string.IsNullOrWhiteSpace(result.PendingGoogleIdToken))
            {
                // New user flow: Backend requires role selection
                PendingGoogleIdToken = result.PendingGoogleIdToken ?? googleAuthResult.IdToken;
                GoogleSelectedRole = null;
                GoogleRoleError = null;
                IsRoleSelectionDialogOpen = true;
            }
            else
            {
                SetError(result.ErrorMessage ?? "Google sign-in was canceled or failed. Please try again.");
            }
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ConfirmGoogleRoleAsync(CancellationToken cancellationToken)
    {
        if (IsLoading) return;

        if (string.IsNullOrWhiteSpace(GoogleSelectedRole) ||
            !UserRoleConstants.IsValidRegistrationRole(GoogleSelectedRole))
        {
            GoogleRoleError = "Please select whether you are a Student or a Landlord.";
            return;
        }

        if (string.IsNullOrWhiteSpace(PendingGoogleIdToken))
        {
            SetError("Google session expired. Please sign in again.");
            IsRoleSelectionDialogOpen = false;
            return;
        }

        try
        {
            IsLoading = true;
            var request = new LoginWithGoogleRequest(PendingGoogleIdToken, GoogleSelectedRole);
            var result = await _authenticationPort.LoginWithGoogleAsync(request, cancellationToken);

            if (result.IsSuccess)
            {
                IsRoleSelectionDialogOpen = false;
                PendingGoogleIdToken = null;
                SetSuccess("Account created and signed in with Google!");

                if (!string.IsNullOrWhiteSpace(result.AccessToken) && result.User != null)
                {
                    await _sessionService.SaveSessionAsync(result.AccessToken, result.RefreshToken ?? string.Empty, result.User, cancellationToken);
                }

                if (OnAuthenticationSuccessHandler != null && result.User != null)
                {
                    await OnAuthenticationSuccessHandler.Invoke(result.User);
                }
            }
            else
            {
                GoogleRoleError = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            GoogleRoleError = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public bool ValidateLoginForm()
    {
        var isValid = true;

        if (string.IsNullOrWhiteSpace(LoginEmail))
        {
            LoginEmailError = "Email address is required.";
            isValid = false;
        }
        else if (LoginEmail.Trim().Length > 254 || !EmailRegex.IsMatch(LoginEmail.Trim()))
        {
            LoginEmailError = "Please enter a valid email address (maximum 254 characters).";
            isValid = false;
        }
        else
        {
            LoginEmailError = null;
        }

        if (string.IsNullOrEmpty(LoginPassword))
        {
            LoginPasswordError = "Password is required.";
            isValid = false;
        }
        else
        {
            LoginPasswordError = null;
        }

        return isValid;
    }

    public bool ValidateSignUpForm()
    {
        var isValid = true;

        // Email validation
        if (string.IsNullOrWhiteSpace(SignUpEmail))
        {
            SignUpEmailError = "Email address is required.";
            isValid = false;
        }
        else if (SignUpEmail.Trim().Length > 254 || !EmailRegex.IsMatch(SignUpEmail.Trim()))
        {
            SignUpEmailError = "Please enter a valid email address (maximum 254 characters).";
            isValid = false;
        }
        else
        {
            SignUpEmailError = null;
        }

        // Password validation
        if (string.IsNullOrEmpty(SignUpPassword))
        {
            SignUpPasswordError = "Password is required.";
            isValid = false;
        }
        else
        {
            var byteCount = Encoding.UTF8.GetByteCount(SignUpPassword);
            if (SignUpPassword.Length < 8 || byteCount > 72)
            {
                SignUpPasswordError = "Password must be at least 8 characters long and not exceed 72 bytes.";
                isValid = false;
            }
            else if (!SignUpPassword.Any(char.IsLetter) || !SignUpPassword.Any(char.IsDigit))
            {
                SignUpPasswordError = "Password must contain at least one letter and one number.";
                isValid = false;
            }
            else
            {
                SignUpPasswordError = null;
            }
        }

        // Confirm Password validation
        if (SignUpPassword != SignUpConfirmPassword)
        {
            SignUpConfirmPasswordError = "Passwords do not match.";
            isValid = false;
        }
        else
        {
            SignUpConfirmPasswordError = null;
        }

        // Role validation
        if (string.IsNullOrWhiteSpace(SelectedRole) || !UserRoleConstants.IsValidRegistrationRole(SelectedRole))
        {
            RoleError = "Please select whether you are a Student or a Landlord.";
            isValid = false;
        }
        else
        {
            RoleError = null;
        }

        return isValid;
    }

    public void ClearStatus()
    {
        ClearErrors();
        IsSuccess = false;
        SuccessMessage = null;
    }

    public void ClearErrors()
    {
        HasError = false;
        ErrorMessage = null;
        LoginEmailError = null;
        LoginPasswordError = null;
        SignUpEmailError = null;
        SignUpPasswordError = null;
        SignUpConfirmPasswordError = null;
        RoleError = null;
        GoogleRoleError = null;
    }

    private void SetError(string message)
    {
        IsSuccess = false;
        SuccessMessage = null;
        HasError = true;
        ErrorMessage = message;
    }

    private void SetSuccess(string message)
    {
        HasError = false;
        ErrorMessage = null;
        IsSuccess = true;
        SuccessMessage = message;
    }
}
