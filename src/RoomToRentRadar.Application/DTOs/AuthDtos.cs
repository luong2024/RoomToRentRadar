namespace RoomToRentRadar.Application.DTOs;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string Role);

public sealed record RegisterUserResult(
    Guid UserId,
    string Email,
    string Role,
    DateTimeOffset CreatedAt);

public sealed record LoginWithEmailCommand(
    string Email,
    string Password);

public sealed record LoginWithGoogleCommand(
    string IdToken,
    string? Role = null);

public sealed record RefreshSessionCommand(
    string RefreshToken);

public sealed record LogoutCommand(
    string RefreshToken);

public sealed record AuthenticationResult(
    Guid UserId,
    string Email,
    string Role,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
