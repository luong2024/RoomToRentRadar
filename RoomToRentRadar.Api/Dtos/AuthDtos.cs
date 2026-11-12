using System.ComponentModel.DataAnnotations;

namespace RoomToRentRadar.Api.Dtos;

// Request/response contracts for /api/auth (US01 / T01.1).
// Validation messages are in Vietnamese because they are shown directly to end users (AC4).
// [ApiController] validates these automatically and returns 400 before the action runs (AC1).

/// <summary>Body of POST /api/auth/register.</summary>
public class RegisterRequest
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(256, ErrorMessage = "Email tối đa 256 ký tự.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
    [MaxLength(100, ErrorMessage = "Mật khẩu tối đa 100 ký tự.")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Optional contact phone.</summary>
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
    public string? Phone { get; set; }

    /// <summary>
    /// "Student" or "Landlord" (case-insensitive). Admin is rejected in the controller
    /// via AppRoles.NormalizeSelfRegisterable, since admins are only created by the seeder.
    /// </summary>
    [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
    public string Role { get; set; } = string.Empty;
}

/// <summary>Body of POST /api/auth/login.</summary>
public class LoginRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string Email { get; set; } = string.Empty;

    // No MinLength here on purpose: a wrong password should get the generic
    // "email hoặc mật khẩu không đúng" message, not a hint about password rules.
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Returned on successful register or login.</summary>
public record AuthResponse(
    string Token,
    DateTime ExpiresAtUtc,
    int UserId,
    string Email,
    string FullName,
    string Role);

/// <summary>
/// Error body used by AuthController for 400/401/409 responses.
/// Message is a single friendly sentence; Errors optionally lists per-field problems.
/// </summary>
public record ErrorResponse(
    string Message,
    IDictionary<string, string[]>? Errors = null);
