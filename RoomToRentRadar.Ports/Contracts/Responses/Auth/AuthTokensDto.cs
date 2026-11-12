namespace RoomToRentRadar.Ports.Contracts.Responses.Auth;

public record AuthTokensDto(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAtUtc,
    DateTime RefreshTokenExpiresAtUtc,
    UserProfileDto User
);

public record IctuSsoUserPayload(
    string Email,
    string FullName,
    string SubjectId,
    string? StudentCode = null
);

public record GoogleUserPayload(
    string Email,
    string FullName,
    string SubjectId
);

public record GeneratedAccessToken(
    string Token,
    string JwtId,
    DateTime ExpiresAtUtc
);

