using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.Ports.Outbound;

public sealed record GeneratedAccessToken(
    string Token,
    DateTimeOffset ExpiresAt);

public sealed record GeneratedRefreshToken(
    string RawToken,
    string TokenHash,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Outbound port for issuing JWT access tokens and cryptographically random refresh tokens.
/// Token lifetimes are controlled centrally via configuration in the adapter implementation.
/// </summary>
public interface ITokenService
{
    GeneratedAccessToken GenerateAccessToken(User user, DateTimeOffset utcNow);

    GeneratedRefreshToken GenerateRefreshToken(DateTimeOffset utcNow);

    string HashRefreshToken(string rawRefreshToken);
}
