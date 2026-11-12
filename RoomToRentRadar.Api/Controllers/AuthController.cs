using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using RoomToRentRadar.Api.Auth;
using RoomToRentRadar.Api.Dtos;
using RoomToRentRadar.Api.Entities;
using RoomToRentRadar.Api.Services;

namespace RoomToRentRadar.Api.Controllers;

/// <summary>
/// Register / login / current-user endpoints for US01 (T01.1).
/// Invalid request bodies (AC1) are rejected by [ApiController] before reaching these actions;
/// Program.cs formats those 400 responses as <see cref="ErrorResponse"/>.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    ILogger<AuthController> logger) : ControllerBase
{
    // One message for "no such email" and "wrong password" so attackers
    // cannot find out which emails are registered (AC4).
    private const string InvalidCredentialsMessage = "Email hoặc mật khẩu không đúng.";

    /// <summary>Creates a Student or Landlord account and signs the user in.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var role = AppRoles.NormalizeSelfRegisterable(request.Role);
        if (role is null)
        {
            return BadRequest(new ErrorResponse(
                "Vai trò không hợp lệ. Chỉ được chọn Student (sinh viên) hoặc Landlord (chủ trọ).",
                new Dictionary<string, string[]> { [nameof(request.Role)] = ["Vai trò không hợp lệ."] }));
        }

        var email = request.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return Conflict(new ErrorResponse("Email này đã được đăng ký. Vui lòng đăng nhập hoặc dùng email khác."));
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
        };

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return IdentityFailure(created, email);
        }

        var addedToRole = await userManager.AddToRoleAsync(user, role);
        if (!addedToRole.Succeeded)
        {
            // Should not happen (roles are seeded). Roll back so the email can be reused.
            logger.LogError("Could not add {Email} to role {Role}: {Errors}",
                email, role, string.Join("; ", addedToRole.Errors.Select(e => e.Code)));
            await userManager.DeleteAsync(user);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponse("Không thể tạo tài khoản lúc này. Vui lòng thử lại sau."));
        }

        logger.LogInformation("New {Role} account registered: user {UserId}", role, user.Id);
        return StatusCode(StatusCodes.Status201Created, BuildAuthResponse(user, role));
    }

    /// <summary>Checks email + password and returns a JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return Unauthorized(new ErrorResponse(InvalidCredentialsMessage));
        }

        // Lockout settings (max attempts, duration) are configured in Program.cs.
        if (await userManager.IsLockedOutAsync(user))
        {
            return Unauthorized(new ErrorResponse(
                "Tài khoản tạm thời bị khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau ít phút."));
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            return Unauthorized(new ErrorResponse(InvalidCredentialsMessage));
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var role = await GetPrimaryRoleAsync(user);
        return Ok(BuildAuthResponse(user, role));
    }

    /// <summary>
    /// Returns the user identified by the Bearer token. Used to verify that the
    /// client attaches "Authorization: Bearer &lt;token&gt;" correctly (AC3).
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me()
    {
        // "sub" is read as-is because Program.cs sets MapInboundClaims = false.
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var user = userId is null ? null : await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            // Valid signature but the account no longer exists.
            return Unauthorized(new ErrorResponse("Phiên đăng nhập không còn hợp lệ. Vui lòng đăng nhập lại."));
        }

        return Ok(new
        {
            userId = user.Id,
            email = user.Email,
            fullName = user.FullName,
            phone = user.PhoneNumber,
            role = User.FindFirstValue("role"),
        });
    }

    private AuthResponse BuildAuthResponse(ApplicationUser user, string role)
    {
        var (token, expiresAtUtc) = tokenService.CreateToken(user, role);
        return new AuthResponse(token, expiresAtUtc, user.Id, user.Email ?? string.Empty, user.FullName, role);
    }

    /// <summary>Each account has exactly one role in this app; take the first.</summary>
    private async Task<string> GetPrimaryRoleAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Count == 0)
        {
            logger.LogWarning("User {UserId} has no role; defaulting to {Role}", user.Id, AppRoles.Student);
            return AppRoles.Student;
        }

        return roles[0];
    }

    /// <summary>Translates Identity error codes into friendly Vietnamese messages (AC4).</summary>
    private IActionResult IdentityFailure(IdentityResult result, string email)
    {
        var codes = result.Errors.Select(e => e.Code).ToList();
        logger.LogInformation("Registration rejected for {Email}: {Codes}", email, string.Join(", ", codes));

        if (codes.Contains(nameof(IdentityErrorDescriber.DuplicateEmail)) ||
            codes.Contains(nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return Conflict(new ErrorResponse("Email này đã được đăng ký. Vui lòng đăng nhập hoặc dùng email khác."));
        }

        var messages = result.Errors
            .Select(e => e.Code switch
            {
                nameof(IdentityErrorDescriber.PasswordTooShort) => "Mật khẩu phải có ít nhất 6 ký tự.",
                nameof(IdentityErrorDescriber.InvalidEmail) => "Email không đúng định dạng.",
                nameof(IdentityErrorDescriber.InvalidUserName) => "Email chứa ký tự không hợp lệ.",
                _ => "Thông tin đăng ký không hợp lệ.",
            })
            .Distinct()
            .ToArray();

        return BadRequest(new ErrorResponse(messages[0], new Dictionary<string, string[]> { ["Identity"] = messages }));
    }
}
