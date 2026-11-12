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
    private AuthViewModel _viewModel = null!;

    [SetUp]
    public void Setup()
    {
        _authPort = new MockAuthPort();
        _googleAdapter = new MockGoogleAdapter();
        _viewModel = new AuthViewModel(_authPort, _googleAdapter);
    }

    [Test]
    public void ToggleForm_ShouldSwitchViewAndClearErrors()
    {
        _viewModel.IsSignUpView = false;
        _viewModel.LoginEmail = "";
        _viewModel.ValidateLoginForm();
        Assert.That(_viewModel.LoginEmailError, Is.Not.Null);

        _viewModel.ToggleForm();

        Assert.That(_viewModel.IsSignUpView, Is.True);
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
    public void ValidateSignUpForm_WhenPasswordExceeds72Utf8Bytes_ShouldSetLengthError()
    {
        _viewModel.SignUpEmail = "student@example.com";
        // 40 2-byte Vietnamese characters + 1 number = 81 UTF-8 bytes
        var longUtf8Password = new string('ế', 40) + "1";
        Assert.That(Encoding.UTF8.GetByteCount(longUtf8Password), Is.GreaterThan(72));

        _viewModel.SignUpPassword = longUtf8Password;
        _viewModel.SignUpConfirmPassword = longUtf8Password;
        _viewModel.SelectedRole = UserRoleConstants.Student;

        var result = _viewModel.ValidateSignUpForm();

        Assert.That(result, Is.False);
        Assert.That(_viewModel.SignUpPasswordError, Is.EqualTo("Password must be at least 8 characters long and not exceed 72 bytes."));
    }

    [Test]
    public void ValidateSignUpForm_WhenPasswordMissingDigits_ShouldSetComplexityError()
    {
        _viewModel.SignUpEmail = "student@example.com";
        _viewModel.SignUpPassword = "PasswordOnly";
        _viewModel.SignUpConfirmPassword = "PasswordOnly";
        _viewModel.SelectedRole = UserRoleConstants.Student;

        var result = _viewModel.ValidateSignUpForm();

        Assert.That(result, Is.False);
        Assert.That(_viewModel.SignUpPasswordError, Is.EqualTo("Password must contain at least one letter and one number."));
    }

    [Test]
    public void ValidateSignUpForm_WhenPasswordMissingLetters_ShouldSetComplexityError()
    {
        _viewModel.SignUpEmail = "student@example.com";
        _viewModel.SignUpPassword = "1234567890";
        _viewModel.SignUpConfirmPassword = "1234567890";
        _viewModel.SelectedRole = UserRoleConstants.Student;

        var result = _viewModel.ValidateSignUpForm();

        Assert.That(result, Is.False);
        Assert.That(_viewModel.SignUpPasswordError, Is.EqualTo("Password must contain at least one letter and one number."));
    }

    [Test]
    public void ValidateSignUpForm_WhenConfirmPasswordDoesNotMatch_ShouldSetMismatchError()
    {
        _viewModel.SignUpEmail = "student@example.com";
        _viewModel.SignUpPassword = "SecurePassword1";
        _viewModel.SignUpConfirmPassword = "DifferentPassword1";
        _viewModel.SelectedRole = UserRoleConstants.Student;

        var result = _viewModel.ValidateSignUpForm();

        Assert.That(result, Is.False);
        Assert.That(_viewModel.SignUpConfirmPasswordError, Is.EqualTo("Passwords do not match."));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("ADMIN")]
    [TestCase("OTHER")]
    public void ValidateSignUpForm_WhenRoleIsInvalidOrEmpty_ShouldSetRoleError(string? invalidRole)
    {
        _viewModel.SignUpEmail = "student@example.com";
        _viewModel.SignUpPassword = "SecurePassword1";
        _viewModel.SignUpConfirmPassword = "SecurePassword1";
        _viewModel.SelectedRole = invalidRole;

        var result = _viewModel.ValidateSignUpForm();

        Assert.That(result, Is.False);
        Assert.That(_viewModel.RoleError, Is.EqualTo("Please select whether you are a Student or a Landlord."));
    }

    [TestCase(UserRoleConstants.Student)]
    [TestCase(UserRoleConstants.Landlord)]
    public void ValidateSignUpForm_WhenAllInputsValid_ShouldReturnTrue(string role)
    {
        _viewModel.SignUpEmail = "valid.user@example.com";
        _viewModel.SignUpPassword = "StrongPassword123";
        _viewModel.SignUpConfirmPassword = "StrongPassword123";
        _viewModel.SelectedRole = role;

        var result = _viewModel.ValidateSignUpForm();

        Assert.That(result, Is.True);
        Assert.That(_viewModel.SignUpEmailError, Is.Null);
        Assert.That(_viewModel.SignUpPasswordError, Is.Null);
        Assert.That(_viewModel.SignUpConfirmPasswordError, Is.Null);
        Assert.That(_viewModel.RoleError, Is.Null);
    }

    [Test]
    public async Task SignInAsync_WhenSuccessful_ShouldSetSuccessState()
    {
        _viewModel.LoginEmail = "User@Example.COM ";
        _viewModel.LoginPassword = "ValidPassword123";

        var user = new AuthUser(Guid.NewGuid(), "user@example.com", "STUDENT", "LOCAL", true);
        _authPort.LoginResult = AuthResult.Success("access-token", "refresh-token", user);

        await _viewModel.SignInAsync(CancellationToken.None);

        Assert.That(_viewModel.IsSuccess, Is.True);
        Assert.That(_viewModel.HasError, Is.False);
        Assert.That(_authPort.LastLoginRequest?.Email, Is.EqualTo("user@example.com")); // Normalized
    }

    [Test]
    public async Task SignUpAsync_WhenSuccessful_ShouldSetSuccessState()
    {
        _viewModel.SignUpEmail = " NewUser@Domain.com";
        _viewModel.SignUpPassword = "ValidPassword1";
        _viewModel.SignUpConfirmPassword = "ValidPassword1";
        _viewModel.SelectedRole = UserRoleConstants.Landlord;

        var user = new AuthUser(Guid.NewGuid(), "newuser@domain.com", "LANDLORD", "LOCAL", true);
        _authPort.RegisterResult = AuthResult.Success(string.Empty, string.Empty, user);

        await _viewModel.SignUpAsync(CancellationToken.None);

        Assert.That(_viewModel.IsSuccess, Is.True);
        Assert.That(_viewModel.HasError, Is.False);
        Assert.That(_authPort.LastRegisterRequest?.Email, Is.EqualTo("newuser@domain.com"));
        Assert.That(_authPort.LastRegisterRequest?.Role, Is.EqualTo("LANDLORD"));
    }

    [Test]
    public async Task GoogleSignInAsync_WhenCanceled_ShouldSetExpectedErrorMessage()
    {
        _googleAdapter.Result = GoogleAuthResult.Canceled();

        await _viewModel.GoogleSignInAsync(CancellationToken.None);

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
    public async Task ConfirmGoogleRoleAsync_WhenRoleSelected_ShouldCompleteRegistration()
    {
        _viewModel.IsRoleSelectionDialogOpen = true;
        _viewModel.PendingGoogleIdToken = "token-123";
        _viewModel.GoogleSelectedRole = UserRoleConstants.Student;

        var user = new AuthUser(Guid.NewGuid(), "google@example.com", "STUDENT", "GOOGLE", true);
        _authPort.GoogleLoginResult = AuthResult.Success("access-token", "refresh-token", user);

        await _viewModel.ConfirmGoogleRoleAsync(CancellationToken.None);

        Assert.That(_viewModel.IsRoleSelectionDialogOpen, Is.False);
        Assert.That(_viewModel.IsSuccess, Is.True);
        Assert.That(_authPort.LastGoogleLoginRequest?.Role, Is.EqualTo("STUDENT"));
    }

    private sealed class MockAuthPort : IAuthenticationPort
    {
        public AuthResult RegisterResult { get; set; } = AuthResult.Success("", "", new AuthUser(Guid.NewGuid(), "", "", "", true));
        public AuthResult LoginResult { get; set; } = AuthResult.Success("", "", new AuthUser(Guid.NewGuid(), "", "", "", true));
        public AuthResult GoogleLoginResult { get; set; } = AuthResult.Success("", "", new AuthUser(Guid.NewGuid(), "", "", "", true));

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
    }

    private sealed class MockGoogleAdapter : IGoogleAuthAdapter
    {
        public GoogleAuthResult Result { get; set; } = GoogleAuthResult.Success("sample-id-token");

        public Task<GoogleAuthResult> AuthenticateWithGoogleAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result);
        }
    }
}
