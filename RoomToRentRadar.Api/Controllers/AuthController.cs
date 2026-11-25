using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using RoomToRentRadar.Abstractions.Auth;
using RoomToRentRadar.Api.Auth;
using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Data;
// "Models" alone would resolve to RoomToRentRadar.Api.Models inside this namespace, so alias the domain type.
using UserAccount = RoomToRentRadar.Models.UserAccount;

namespace RoomToRentRadar.Api.Controllers
{
    /// <summary>
    /// US01 / T01.1 — "C" of the MVC Web API: registration, login and current user, secured with JWT.
    /// Request validation (required fields, email, phone, password length) is done by [ApiController]
    /// from the DataAnnotations in AuthModels; this controller only applies business rules.
    /// It depends on IAuthService only, so the user store (custom SQLite AuthService today,
    /// ASP.NET Core Identity later if required) can be swapped without touching this file or the MAUI app.
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    [Produces("application/json")]
    public sealed class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;
        private readonly JwtTokenService _tokens;
        private readonly UserRepository _users;

        public AuthController(IAuthService auth, JwtTokenService tokens, UserRepository users)
        {
            _auth = auth;
            _tokens = tokens;
            _users = users;
        }

        /// <summary>POST /api/auth/register → 201 + token | 400 invalid data, Admin role or email already used.</summary>
        [HttpPost("register")]
        [AllowAnonymous]
        [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            // Admin accounts are never self-registered; only Student or Landlord.
            var role = NormalizeSelfRegisterRole(request.Role);
            if (role is null)
            {
                return BadRequest(new ErrorResponse("Vai trò chỉ được là Student hoặc Landlord."));
            }

            var user = await _auth.RegisterAsync(request.FullName, request.Email, request.Password, request.Phone, role);

            // The IAuthService contract returns null for invalid data or an email that is already used.
            if (user is null)
            {
                return BadRequest(new ErrorResponse("Không thể đăng ký: email không hợp lệ hoặc đã được sử dụng."));
            }

            // Signed in right away: the new account receives a token like a normal login.
            return CreatedAtAction(nameof(Me), CreateTokenResponse(user));
        }

        /// <summary>POST /api/auth/login → 200 + token | 401 wrong email or password.</summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var user = await _auth.LoginAsync(request.Email, request.Password);

            // Same 401 for "unknown email" and "wrong password" so callers cannot probe which emails exist.
            if (user is null)
            {
                return Unauthorized(new ErrorResponse("Email hoặc mật khẩu không đúng."));
            }

            return Ok(CreateTokenResponse(user));
        }

        /// <summary>GET /api/auth/me (Bearer token) → 200 current user | 401 no/invalid/expired token | 404 account deleted.</summary>
        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType<AuthUserResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Me()
        {
            if (!TryGetUserId(User, out var userId))
            {
                return Unauthorized(new ErrorResponse("Token không hợp lệ."));
            }

            // Read fresh data from the database so a changed name/role is visible without a new login.
            var user = await _users.GetByIdAsync(userId);
            return user is null
                ? NotFound(new ErrorResponse("Tài khoản không còn tồn tại."))
                : Ok(ToUserResponse(user));
        }

        /// <summary>
        /// Reads the caller's user id from the token's "sub" claim.
        /// Other controllers must use this (never IAuthService.CurrentUser, which is shared by all requests).
        /// </summary>
        public static bool TryGetUserId(ClaimsPrincipal principal, out int userId)
        {
            userId = 0;
            var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return int.TryParse(sub, out userId) && userId > 0;
        }

        private AuthTokenResponse CreateTokenResponse(UserAccount user)
        {
            var token = _tokens.CreateToken(user);
            return new AuthTokenResponse(token.Token, "Bearer", token.ExpiresAtUtc, ToUserResponse(user));
        }

        private static string? NormalizeSelfRegisterRole(string? role)
        {
            string[] allowed = [Constants.UserRoles.Student, Constants.UserRoles.Landlord];
            return allowed.FirstOrDefault(r => string.Equals(r, role?.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Maps the domain user to a safe response: PasswordHash is never sent to the client.</summary>
        private static AuthUserResponse ToUserResponse(UserAccount user) =>
            new(user.Id, user.FullName, user.Email, user.Phone, user.Role, user.SelectedCampus);
    }
}
