using System.ComponentModel.DataAnnotations;

namespace RoomToRentRadar.Api.Models
{
    // ============================================================================
    // US01 / T01.1 — "M" of the MVC Web API: request/response models for AuthController.
    // [ApiController] validates the DataAnnotations below automatically and answers 400
    // before the action runs, so the controller only handles business rules.
    // ============================================================================

    /// <summary>Body of POST /api/auth/register.</summary>
    public sealed record RegisterRequest(
        [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
        [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
        string FullName,

        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        string Email,

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        [MinLength(AuthRules.MinPasswordLength, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
        string Password,

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        string Phone,

        [Required(ErrorMessage = "Vui lòng chọn vai trò Student hoặc Landlord.")]
        string Role);

    /// <summary>Body of POST /api/auth/login.</summary>
    public sealed record LoginRequest(
        [Required(ErrorMessage = "Vui lòng nhập email.")]
        string Email,

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        string Password);

    /// <summary>User data returned to the client. Never contains the password hash.</summary>
    public sealed record AuthUserResponse(int Id, string FullName, string Email, string Phone, string Role, string SelectedCampus);

    /// <summary>
    /// Returned by register/login. The client stores AccessToken (MAUI: SecureStorage) and sends it as
    /// "Authorization: Bearer {AccessToken}" until ExpiresAtUtc, then logs in again.
    /// </summary>
    public sealed record AuthTokenResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc, AuthUserResponse User);

    /// <summary>
    /// Error body with a Vietnamese message ready for the UI.
    /// Errors (optional) maps a field name to its validation messages.
    /// </summary>
    public sealed record ErrorResponse(string Message, IDictionary<string, string[]>? Errors = null);

    /// <summary>Shared auth constants used by the models and AuthController.</summary>
    public static class AuthRules
    {
        public const int MinPasswordLength = 6;
    }
}
