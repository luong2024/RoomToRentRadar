using System.Text;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Models.Authentication;
using RoomToRentRadar.Features.Authentication.ViewModels;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class AuthViewModelTests
{
    private MockAuthPort _authPort = null!;
    private MockGoogleAdapter _googleAdapter = null!;
    private MockSessionService _sessionService = null!;
    private AuthViewModel _viewModel = null!;

    [SetUp]
    public void Setup()
    {
        _authPort = new MockAuthPort();
        _googleAdapter = new MockGoogleAdapter();
        _sessionService = new MockSessionService();
        _viewModel = new AuthViewModel(_authPort, _googleAdapter, _sessionService);
    }

    [Test]
    public void ToggleForm_ShouldSwitchViewAndClearErrorsAndInputs()
    {
        _viewModel.IsSignUpView = false;
        _viewModel.LoginEmail = "wrong@example.com";
        _viewModel.LoginPassword = "WrongPassword123";
        _viewModel.ValidateLoginForm();

        _viewModel.ToggleForm();

        Assert.That(_viewModel.IsSignUpView, Is.True);
        Assert.That(_viewModel.LoginEmail, Is.Empty);
        Assert.That(_viewModel.LoginPassword, Is.Empty);
        Assert.That(_viewModel.LoginEmailError, Is.Null);
    }

    [Test]
    public void ValidateLoginForm_WhenEmailIsEmpty_ShouldSetRequiredError()
    {
        _viewModel.LoginEmail = "   ";
        _viewModel.LoginPassword = "ValidPassword123";

        var result = _viewModel.ValidateLoginForm();

        Assert.That(result, Is.False);
        Assert.That(_viewModel.LoginEmailError, Is.EqualTo("Email address is required."));
    }

    [TestCase("invalid-email")]
    [TestCase("missingatsign.com")]
    [TestCase("@nodomain.com")]
    public void ValidateLoginForm_WhenEmailIsInvalidFormat_ShouldSetFormatError(string invalidEmail)
    {
        _viewModel.LoginEmail = invalidEmail;
        _viewModel.LoginPassword = "ValidPassword123";

        var result = _viewModel.ValidateLoginForm();

        Assert.That(result, Is.False);
        Assert.That(_viewModel.LoginEmailError, Is.EqualTo("Please enter a valid email address (maximum 254 characters)."));
    }

    [Test]
    public void ValidateLoginForm_WhenEmailExceeds254Characters_ShouldSetFormatError()
    {
        var longEmail = new string('a', 245) + "@example.com"; // > 254
        _viewModel.LoginEmail = longEmail;
        _viewModel.LoginPassword = "ValidPassword123";

        var result = _viewModel.ValidateLoginForm();

        Assert.That(result, Is.False);
        Assert.That(_viewModel.LoginEmailError, Is.EqualTo("Please enter a valid email address (maximum 254 characters)."));
    }

    [Test]
    public void ValidateLoginForm_WhenPasswordIsEmpty_ShouldSetRequiredError()
    {
        _viewModel.LoginEmail = "test@example.com";
        _viewModel.LoginPassword = "";

        var result = _viewModel.ValidateLoginForm();

        Assert.That(result, Is.False);
        Assert.That(_viewModel.LoginPasswordError, Is.EqualTo("Password is required."));
    }

    [Test]
    public void ValidateSignUpForm_WhenPasswordLessThan8Chars_ShouldSetLengthError()
    {
        _viewModel.SignUpEmail = "student@example.com";
        _viewModel.SignUpPassword = "Pass1";
        _viewModel.SignUpConfirmPassword = "Pass1";
        _viewModel.SelectedRole = UserRoleConstants.Student;

        var result = _viewModel.ValidateSignUpForm();

        Assert.That(result, Is.False);
        Assert.That(_viewModel.SignUpPasswordError, Is.EqualTo("Password must be at least 8 characters long and not exceed 72 bytes."));
    }

    [Test]
    public void ValidateSignUpForm_WhenPasswordExceeds72Bytes_ShouldSetLengthError()
    {
        _viewModel.SignUpEmail = "student@example.com";
        // 73 ASCII characters = 73 bytes
        _viewModel.SignUpPassword = new string('a', 72) + "1";
        _viewModel.SignUpConfirmPassword = _viewModel.SignUpPassword;
        _viewModel.SelectedRole = UserRoleConstants.Student;

        var result = _viewModel.ValidateSignUpForm();

        Assert.That(result, Is.False);
        Assert.That(_viewModel.SignUpPasswordError, Is.EqualTo("Password must be at least 8 characters long and not exceed 72 bytes."));
    }

    [TestCase("passwordonly")]
    [TestCase("12345678")]
    [TestCase("!@#$%^&*")]
    public void ValidateSignUpForm_WhenPasswordMissingDigitOrLetter_ShouldSetComplexityError(string simplePassword)
    {
        _viewModel.SignUpEmail = "student@example.com";
        _viewModel.SignUpPassword = simplePassword;
        _viewModel.SignUpConfirmPassword = simplePassword;
        _viewModel.SelectedRole = UserRoleConstants.Student;

        var result = _viewModel.ValidateSignUpForm();

        Assert.That(result, Is.False);
        Assert.That(_viewModel.SignUpPasswordError, Is.EqualTo("Password must contain at least one letter and one number."));
    }

    [Test]
    public void ValidateSignUpForm_WhenPasswordsDoNotMatch_ShouldSetMismatchError()
    {
        _viewModel.SignUpEmail = "student@example.com";
        _viewModel.SignUpPassword = "ValidPassword1";
        _viewModel.SignUpConfirmPassword = "DifferentPassword2";
        _viewModel.SelectedRole = UserRoleConstants.Student;

        var result = _viewModel.ValidateSignUpForm();

        Assert.That(result, Is.False);
        Assert.That(_viewModel.SignUpConfirmPasswordError, Is.EqualTo("Passwords do not match."));
    }

    [Test]
    public void ValidateSignUpForm_WhenRoleNotSelected_ShouldSetRoleError()
    {
        _viewModel.SignUpEmail = "student@example.com";
        _viewModel.SignUpPassword = "ValidPassword1";
        _viewModel.SignUpConfirmPassword = "ValidPassword1";
        _viewModel.SelectedRole = null;

        var result = _viewModel.ValidateSignUpForm();

        Assert.That(result, Is.False);
        Assert.That(_viewModel.RoleError, Is.EqualTo("Please select whether you are a Student or a Landlord."));
    }

    [Test]
    public void ValidateSignUpForm_WhenValid_ShouldReturnTrueAndHaveNoErrors()
    {
        _viewModel.SignUpEmail = "student@example.com";
        _viewModel.SignUpPassword = "ValidPassword1";
        _viewModel.SignUpConfirmPassword = "ValidPassword1";
        _viewModel.SelectedRole = UserRoleConstants.Student;

        var result = _viewModel.ValidateSignUpForm();

        Assert.That(result, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(_viewModel.SignUpEmailError, Is.Null);
            Assert.That(_viewModel.SignUpPasswordError, Is.Null);
            Assert.That(_viewModel.SignUpConfirmPasswordError, Is.Null);
            Assert.That(_viewModel.RoleError, Is.Null);
        });
    }

    [Test]
    public async Task SignInAsync_WhenSuccessful_ShouldSaveSessionAndInvokeOnAuthenticationSuccessHandler()
    {
        _viewModel.LoginEmail = "student@example.com";
        _viewModel.LoginPassword = "ValidPassword1";

        var user = new AuthUser(Guid.NewGuid(), "student@example.com", "STUDENT", "LOCAL", true);
        _authPort.LoginResult = AuthResult.Success("test-access-token", "test-refresh-token", user);

        AuthUser? callbackUser = null;
        _viewModel.OnAuthenticationSuccessHandler = u =>
        {
            callbackUser = u;
            return Task.CompletedTask;
        };

        await _viewModel.SignInAsync(CancellationToken.None);

        Assert.That(_viewModel.IsSuccess, Is.True);
        Assert.That(_viewModel.ErrorMessage, Is.Null);
        Assert.That(_sessionService.SavedAccessToken, Is.EqualTo("test-access-token"));
        Assert.That(_sessionService.SavedRefreshToken, Is.EqualTo("test-refresh-token"));
        Assert.That(_sessionService.SavedUser, Is.EqualTo(user));
        Assert.That(callbackUser, Is.EqualTo(user));
    }

    [Test]
    public async Task SignInAsync_WhenAuthFails_ShouldSetError()
    {
        _viewModel.LoginEmail = "student@example.com";
        _viewModel.LoginPassword = "ValidPassword1";
        _authPort.LoginResult = AuthResult.Failed("Invalid credentials.");

        await _viewModel.SignInAsync(CancellationToken.None);

        Assert.That(_viewModel.IsSuccess, Is.False);
        Assert.That(_viewModel.HasError, Is.True);
        Assert.That(_viewModel.ErrorMessage, Is.EqualTo("Invalid credentials."));
        Assert.That(_sessionService.SavedAccessToken, Is.Null);
    }

    [Test]
    public async Task SignUpAsync_WhenSuccessful_ShouldClearAllFormFieldsAndCallFlipToLoginHandler()
    {
        // Simulate previous failed login input left on the form
        _viewModel.LoginEmail = "failedlogin@example.com";
        _viewModel.LoginPassword = "OldPassword123";

        // Fill sign up form
        _viewModel.SignUpEmail = "student@example.com";
        _viewModel.SignUpPassword = "ValidPassword1";
        _viewModel.SignUpConfirmPassword = "ValidPassword1";
        _viewModel.SelectedRole = UserRoleConstants.Landlord;
        _authPort.RegisterResult = AuthResult.Success("", "", new AuthUser(Guid.NewGuid(), "student@example.com", "LANDLORD", "LOCAL", true));

        var flipCalled = false;
        _viewModel.FlipToLoginHandler = () =>
        {
            flipCalled = true;
            return Task.CompletedTask;
        };

        await _viewModel.SignUpAsync(CancellationToken.None);

        Assert.That(_viewModel.IsSuccess, Is.True);
        Assert.That(_viewModel.SuccessMessage, Does.Contain("Account created successfully"));
        Assert.That(flipCalled, Is.True);

        // Verification: Both Login and Register form inputs are completely wiped
        Assert.That(_viewModel.LoginEmail, Is.Empty);
        Assert.That(_viewModel.LoginPassword, Is.Empty);
        Assert.That(_viewModel.SignUpEmail, Is.Empty);
        Assert.That(_viewModel.SignUpPassword, Is.Empty);
        Assert.That(_viewModel.SignUpConfirmPassword, Is.Empty);
        Assert.That(_viewModel.SelectedRole, Is.Null);
    }

    [Test]
    public async Task SignUpAsync_WhenFailed_ShouldRetainSignUpInputsAndNotCallFlip()
    {
        _viewModel.SignUpEmail = "existing@example.com";
        _viewModel.SignUpPassword = "ValidPassword1";
        _viewModel.SignUpConfirmPassword = "ValidPassword1";
        _viewModel.SelectedRole = UserRoleConstants.Student;
        _authPort.RegisterResult = AuthResult.Failed("Email is already registered.");

        var flipCalled = false;
        _viewModel.FlipToLoginHandler = () =>
        {
            flipCalled = true;
            return Task.CompletedTask;
        };

        await _viewModel.SignUpAsync(CancellationToken.None);

        Assert.That(_viewModel.IsSuccess, Is.False);
        Assert.That(_viewModel.HasError, Is.True);
        Assert.That(_viewModel.ErrorMessage, Is.EqualTo("Email is already registered."));
        Assert.That(flipCalled, Is.False);

        // Inputs must be preserved on failure so user can edit them
        Assert.That(_viewModel.SignUpEmail, Is.EqualTo("existing@example.com"));
        Assert.That(_viewModel.SignUpPassword, Is.EqualTo("ValidPassword1"));
        Assert.That(_viewModel.SignUpConfirmPassword, Is.EqualTo("ValidPassword1"));
        Assert.That(_viewModel.SelectedRole, Is.EqualTo(UserRoleConstants.Student));
    }

    [Test]
    public async Task GoogleSignInAsync_WhenSuccessful_ShouldSaveSessionAndInvokeCallback()
    {
        var user = new AuthUser(Guid.NewGuid(), "google@example.com", "STUDENT", "GOOGLE", true);
        _googleAdapter.Result = GoogleAuthResult.Success("google-id-token-123");
        _authPort.GoogleLoginResult = AuthResult.Success("google-access-token", "google-refresh-token", user);

        AuthUser? callbackUser = null;
        _viewModel.OnAuthenticationSuccessHandler = u =>
        {
            callbackUser = u;
            return Task.CompletedTask;
        };

        await _viewModel.GoogleSignInAsync(CancellationToken.None);

        Assert.That(_viewModel.IsSuccess, Is.True);
        Assert.That(_sessionService.SavedAccessToken, Is.EqualTo("google-access-token"));
        Assert.That(callbackUser, Is.EqualTo(user));
    }

    [Test]
    public async Task GoogleSignInAsync_WhenAdapterFails_ShouldSetError()
    {
        _googleAdapter.Result = GoogleAuthResult.Failed();

        await _viewModel.GoogleSignInAsync(CancellationToken.None);

        Assert.That(_viewModel.IsSuccess, Is.False);
        Assert.That(_viewModel.HasError, Is.True);
        Assert.That(_viewModel.ErrorMessage, Is.EqualTo("Google sign-in was canceled or failed. Please try again."));
    }

    [Test]
    public async Task GoogleSignInAsync_WhenBackendRequiresRoleSelection_ShouldOpenDialog()
    {
        _googleAdapter.Result = GoogleAuthResult.Success("google-id-token-123");
        _authPort.GoogleLoginResult = AuthResult.NeedRoleSelection("google-id-token-123");

        await _viewModel.GoogleSignInAsync(CancellationToken.None);

        Assert.That(_viewModel.IsRoleSelectionDialogOpen, Is.True);
        Assert.That(_viewModel.PendingGoogleIdToken, Is.EqualTo("google-id-token-123"));
        Assert.That(_viewModel.GoogleSelectedRole, Is.Null);
    }

    [Test]
    public async Task ConfirmGoogleRoleAsync_WhenRoleNotSelected_ShouldSetError()
    {
        _viewModel.IsRoleSelectionDialogOpen = true;
        _viewModel.PendingGoogleIdToken = "token-123";
        _viewModel.GoogleSelectedRole = null;

        await _viewModel.ConfirmGoogleRoleAsync(CancellationToken.None);

        Assert.That(_viewModel.GoogleRoleError, Is.EqualTo("Please select whether you are a Student or a Landlord."));
    }

    [Test]
    public async Task ConfirmGoogleRoleAsync_WhenRoleSelected_ShouldCompleteRegistrationAndSaveSession()
    {
        _viewModel.IsRoleSelectionDialogOpen = true;
        _viewModel.PendingGoogleIdToken = "token-123";
        _viewModel.GoogleSelectedRole = UserRoleConstants.Student;

        var user = new AuthUser(Guid.NewGuid(), "google@example.com", "STUDENT", "GOOGLE", true);
        _authPort.GoogleLoginResult = AuthResult.Success("access-token", "refresh-token", user);

        AuthUser? callbackUser = null;
        _viewModel.OnAuthenticationSuccessHandler = u =>
        {
            callbackUser = u;
            return Task.CompletedTask;
        };

        await _viewModel.ConfirmGoogleRoleAsync(CancellationToken.None);

        Assert.That(_viewModel.IsRoleSelectionDialogOpen, Is.False);
        Assert.That(_viewModel.IsSuccess, Is.True);
        Assert.That(_authPort.LastGoogleLoginRequest?.Role, Is.EqualTo("STUDENT"));
        Assert.That(_sessionService.SavedAccessToken, Is.EqualTo("access-token"));
        Assert.That(callbackUser, Is.EqualTo(user));
    }

    private sealed class MockAuthPort : IAuthenticationPort
    {
        public AuthResult RegisterResult { get; set; } = AuthResult.Success("", "", new AuthUser(Guid.NewGuid(), "", "", "", true));
        public AuthResult LoginResult { get; set; } = AuthResult.Success("", "", new AuthUser(Guid.NewGuid(), "", "", "", true));
        public AuthResult GoogleLoginResult { get; set; } = AuthResult.Success("", "", new AuthUser(Guid.NewGuid(), "", "", "", true));
        public AuthResult RefreshResult { get; set; } = AuthResult.Success("", "", new AuthUser(Guid.NewGuid(), "", "", "", true));
        public bool LogoutResult { get; set; } = true;

        public RegisterUserRequest? LastRegisterRequest { get; private set; }
        public LoginWithEmailRequest? LastLoginRequest { get; private set; }
        public LoginWithGoogleRequest? LastGoogleLoginRequest { get; private set; }

        public Task<AuthResult> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default)
        {
            LastRegisterRequest = request;
            return Task.FromResult(RegisterResult);
        }

        public Task<AuthResult> LoginWithEmailAsync(LoginWithEmailRequest request, CancellationToken cancellationToken = default)
        {
            LastLoginRequest = request;
            return Task.FromResult(LoginResult);
        }

        public Task<AuthResult> LoginWithGoogleAsync(LoginWithGoogleRequest request, CancellationToken cancellationToken = default)
        {
            LastGoogleLoginRequest = request;
            return Task.FromResult(GoogleLoginResult);
        }

        public Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RefreshResult);
        }

        public Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(LogoutResult);
        }
    }

    private sealed class MockGoogleAdapter : IGoogleAuthAdapter
    {
        public GoogleAuthResult Result { get; set; } = GoogleAuthResult.Success("sample-id-token");

        public Task<GoogleAuthResult> AuthenticateWithGoogleAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result);
        }
    }

    private sealed class MockSessionService : IAuthenticationSessionService
    {
        public string? SavedAccessToken { get; private set; }
        public string? SavedRefreshToken { get; private set; }
        public AuthUser? SavedUser { get; private set; }

        public bool IsAuthenticated => SavedUser != null && !string.IsNullOrWhiteSpace(SavedAccessToken);
        public AuthUser? CurrentUser => SavedUser;

        public event EventHandler<AuthUser?>? CurrentUserChanged;

        public Task SaveSessionAsync(string accessToken, string refreshToken, AuthUser user, CancellationToken cancellationToken = default)
        {
            SavedAccessToken = accessToken;
            SavedRefreshToken = refreshToken;
            SavedUser = user;
            CurrentUserChanged?.Invoke(this, user);
            return Task.CompletedTask;
        }

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(SavedAccessToken);

        public Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(SavedRefreshToken);

        public Task<bool> TryRestoreSessionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(IsAuthenticated);

        public Task<bool> RefreshTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task ClearSessionAsync(CancellationToken cancellationToken = default)
        {
            SavedAccessToken = null;
            SavedRefreshToken = null;
            SavedUser = null;
            CurrentUserChanged?.Invoke(this, null);
            return Task.CompletedTask;
        }
    }
}
