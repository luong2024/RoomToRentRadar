namespace RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;

public sealed record RegisterUserRequest(
    string Email,
    string Password,
    string Role);

public sealed record RegisterUserResponse(
    Guid UserId,
    string Email,
    string Role,
    DateTimeOffset CreatedAt);

public sealed record LoginWithEmailRequest(
    string Email,
    string Password);

public sealed record LoginWithGoogleRequest(
    string IdToken,
    string? Role = null);

public sealed record RefreshSessionRequest(
    string RefreshToken);

public sealed record LogoutRequest(
    string RefreshToken);

public sealed record AuthSessionResponse(
    Guid UserId,
    string Email,
    string Role,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
