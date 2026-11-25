using System.Net.Mail;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Abstractions.Auth;
using RoomToRentRadar.Services.Api;

namespace RoomToRentRadar.Features.Auth
{
    /// <summary>A role the user can pick when registering (Admin is never self-registered).</summary>
    public sealed record RoleOption(string Value, string DisplayName);

    /// <summary>
    /// US01 / T01.2: shared PageModel of LoginPage and RegisterPage (each page gets its own transient instance).
    /// AC1: validates email format and password length on the device before calling the API.
    /// AC4: shows a friendly Vietnamese error (local validation, or the API message via <see cref="IAuthFeedback"/>).
    /// The token itself is stored and attached by ApiAuthService / AuthTokenHandler (AC3).
    /// </summary>
    public partial class AuthPageModel : ObservableObject
    {
        private const int MinPasswordLength = 6;

        private readonly IAuthService _authService;
        private readonly IAuthFeedback _authFeedback;
        private readonly ILogger<AuthPageModel> _logger;

        public AuthPageModel(IAuthService authService, IAuthFeedback authFeedback, ILogger<AuthPageModel> logger)
        {
            _authService = authService;
            _authFeedback = authFeedback;
            _logger = logger;
            SelectedRole = RoleOptions[0];
        }

        public IReadOnlyList<RoleOption> RoleOptions { get; } =
        [
            new(Constants.UserRoles.Student, "Sinh viên"),
            new(Constants.UserRoles.Landlord, "Chủ trọ"),
        ];

        [ObservableProperty]
        public partial string Email { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Password { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ConfirmPassword { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string FullName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Phone { get; set; } = string.Empty;

        [ObservableProperty]
        public partial RoleOption? SelectedRole { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasError))]
        public partial string? ErrorMessage { get; set; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        public partial bool IsBusy { get; set; }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        private bool CanSubmit() => !IsBusy;

        [RelayCommand(CanExecute = nameof(CanSubmit))]
        private async Task LoginAsync()
        {
            ErrorMessage = ValidateEmail() ?? ValidatePassword();
            if (HasError)
            {
                return;
            }

            await SubmitAsync(() => _authService.LoginAsync(Email, Password));
        }

        [RelayCommand(CanExecute = nameof(CanSubmit))]
        private async Task RegisterAsync()
        {
            ErrorMessage = ValidateFullName() ?? ValidateEmail() ?? ValidatePhone() ?? ValidatePassword() ?? ValidateConfirmPassword()
                ?? (SelectedRole is null ? "Vui lòng chọn vai trò Sinh viên hoặc Chủ trọ." : null);
            if (HasError)
            {
                return;
            }

            await SubmitAsync(() => _authService.RegisterAsync(FullName, Email, Password, Phone, SelectedRole!.Value));
        }

        [RelayCommand]
        private Task GoToRegisterAsync() => Shell.Current.GoToAsync(Constants.Routes.Register);

        [RelayCommand]
        private Task GoToLoginAsync() => Shell.Current.GoToAsync("..");

        /// <summary>Calls the API once (IsBusy blocks double taps); on success goes to Home, otherwise shows the reason.</summary>
        private async Task SubmitAsync(Func<Task<UserAccount?>> authCall)
        {
            if (IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                ErrorMessage = null;

                var user = await authCall();
                if (user is null)
                {
                    ErrorMessage = _authFeedback.LastErrorMessage ?? "Đăng nhập không thành công. Vui lòng thử lại.";
                    return;
                }

                Password = string.Empty;
                ConfirmPassword = string.Empty;
                await Shell.Current.GoToAsync($"//{Constants.Routes.Home}");
            }
            catch (Exception ex)
            {
                // ApiAuthService already turns network/API errors into messages; this only catches the unexpected.
                _logger.LogError(ex, "Unexpected error during sign-in/registration.");
                ErrorMessage = "Đã có lỗi xảy ra. Vui lòng thử lại sau.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ---------------------------------------------------------------------
        // AC1: client-side validation (the API validates again on the server)
        // ---------------------------------------------------------------------

        private string? ValidateEmail()
        {
            var email = Email?.Trim() ?? string.Empty;
            if (email.Length == 0)
            {
                return "Vui lòng nhập email.";
            }

            return MailAddress.TryCreate(email, out var address) && address.Address == email && email.Contains('.')
                ? null
                : "Email không đúng định dạng (ví dụ: sinhvien@ictu.edu.vn).";
        }

        private string? ValidatePassword() =>
            string.IsNullOrEmpty(Password) || Password.Length < MinPasswordLength
                ? $"Mật khẩu phải có ít nhất {MinPasswordLength} ký tự."
                : null;

        private string? ValidateConfirmPassword() =>
            Password == ConfirmPassword ? null : "Mật khẩu nhập lại không khớp.";

        private string? ValidateFullName() =>
            string.IsNullOrWhiteSpace(FullName) ? "Vui lòng nhập họ tên." : null;

        private string? ValidatePhone()
        {
            var digits = (Phone ?? string.Empty).Trim().Replace(" ", string.Empty);
            return digits.Length is >= 9 and <= 11 && digits.All(char.IsDigit)
                ? null
                : "Số điện thoại không hợp lệ (9–11 chữ số).";
        }
    }
}
