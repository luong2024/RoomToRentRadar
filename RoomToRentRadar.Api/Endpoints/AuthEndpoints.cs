using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using RoomToRentRadar.Abstractions.Auth;
using RoomToRentRadar.Api.Auth;
using RoomToRentRadar.Data;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Api.Endpoints
{
    /// <summary>
    /// TASK OWNER: T01.1 (Nguyễn Đăng Quang — US01: Build ASP.NET Core Identity &amp; JWT Token API).
    /// Only T01.1 edits this file. Do NOT edit Program.cs.
    /// Minimal API over <see cref="IAuthService"/>:
    ///   POST /api/auth/register, POST /api/auth/login → user + JWT access token,
    ///   GET  /api/auth/me (requires "Authorization: Bearer &lt;token&gt;").
    /// Other endpoints get the caller from the token's "sub" claim, never from IAuthService.CurrentUser
    /// (that property is shared by all requests in a web server).
    /// </summary>
    public static class AuthEndpoints
    {
        public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/auth");

            // POST /api/auth/register → 201 Created | 400 Bad Request
            group.MapPost("/register", async (RegisterRequest request, IAuthService auth, JwtTokenService tokens) =>
            {
                if (request is null
                    || string.IsNullOrWhiteSpace(request.FullName)
                    || string.IsNullOrWhiteSpace(request.Email)
                    || string.IsNullOrWhiteSpace(request.Phone))
                {
                    return Results.BadRequest(new AuthErrorResponse("Vui lòng nhập đầy đủ họ tên, email và số điện thoại."));
                }

                if (string.IsNullOrEmpty(request.Password) || request.Password.Length < MinPasswordLength)
                {
                    return Results.BadRequest(new AuthErrorResponse($"Mật khẩu phải có ít nhất {MinPasswordLength} ký tự."));
                }

                // Admin accounts are never self-registered; only Student or Landlord.
                var role = NormalizeSelfRegisterRole(request.Role);
                if (role is null)
                {
                    return Results.BadRequest(new AuthErrorResponse("Vai trò chỉ được là Student hoặc Landlord."));
                }

                var user = await auth.RegisterAsync(request.FullName, request.Email, request.Password, request.Phone, role);

                // The contract returns null for invalid data or an email that is already used.
                return user is null
                    ? Results.BadRequest(new AuthErrorResponse("Không thể đăng ký: email không hợp lệ hoặc đã được sử dụng."))
                    : Results.Created("/api/auth/me", ToTokenResponse(user, tokens)); // signed in right away
            })
            .WithName("Register");

            // POST /api/auth/login → 200 OK | 400 Bad Request | 401 Unauthorized
            group.MapPost("/login", async (LoginRequest request, IAuthService auth, JwtTokenService tokens) =>
            {
                if (request is null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
                {
                    return Results.BadRequest(new AuthErrorResponse("Vui lòng nhập email và mật khẩu."));
                }

                var user = await auth.LoginAsync(request.Email, request.Password);

                // Same 401 for "unknown email" and "wrong password" so callers cannot probe which emails exist.
                return user is null
                    ? Results.Json(new AuthErrorResponse("Email hoặc mật khẩu không đúng."), statusCode: StatusCodes.Status401Unauthorized)
                    : Results.Ok(ToTokenResponse(user, tokens));
            })
            .WithName("Login");

            // GET /api/auth/me → 200 OK | 401 Unauthorized (no/invalid/expired token) | 404 (account deleted)
            group.MapGet("/me", async (ClaimsPrincipal principal, UserRepository users) =>
            {
                if (!TryGetUserId(principal, out var userId))
                {
                    return Results.Json(new AuthErrorResponse("Token không hợp lệ."), statusCode: StatusCodes.Status401Unauthorized);
                }

                // Read fresh data from SQLite so a changed name/role is reflected without a new login.
                var user = await users.GetByIdAsync(userId);
                return user is null
                    ? Results.NotFound(new AuthErrorResponse("Tài khoản không còn tồn tại."))
                    : Results.Ok(ToResponse(user));
            })
            .RequireAuthorization()
            .WithName("Me");

            return app;
        }

        /// <summary>Reads the user id from the token's "sub" claim. Other task endpoints can copy this pattern.</summary>
        public static bool TryGetUserId(ClaimsPrincipal principal, out int userId)
        {
            userId = 0;
            var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return int.TryParse(sub, out userId) && userId > 0;
        }

        private static AuthTokenResponse ToTokenResponse(UserAccount user, JwtTokenService tokens)
        {
            var token = tokens.CreateToken(user);
            return new AuthTokenResponse(token.Token, "Bearer", token.ExpiresAtUtc, ToResponse(user));
        }

        private const int MinPasswordLength = 6;

        private static string? NormalizeSelfRegisterRole(string? role)
        {
            string[] allowed = [Constants.UserRoles.Student, Constants.UserRoles.Landlord];
            return allowed.FirstOrDefault(r => string.Equals(r, role?.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Maps the domain user to a safe response: PasswordHash is never sent to the client.</summary>
        private static AuthUserResponse ToResponse(UserAccount user) =>
            new(user.Id, user.FullName, user.Email, user.Phone, user.Role, user.SelectedCampus);
    }

    /// <summary>Body of POST /api/auth/register.</summary>
    public sealed record RegisterRequest(string FullName, string Email, string Password, string Phone, string Role);

    /// <summary>Body of POST /api/auth/login.</summary>
    public sealed record LoginRequest(string Email, string Password);

    /// <summary>User data returned to the client (no password hash).</summary>
    public sealed record AuthUserResponse(int Id, string FullName, string Email, string Phone, string Role, string SelectedCampus);

    /// <summary>
    /// Returned by register/login. The client stores AccessToken (e.g. SecureStorage) and sends it as
    /// "Authorization: Bearer {AccessToken}" until ExpiresAtUtc, then logs in again.
    /// </summary>
    public sealed record AuthTokenResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc, AuthUserResponse User);

    /// <summary>Error body with a Vietnamese message ready for the UI.</summary>
    public sealed record AuthErrorResponse(string Message);
}
