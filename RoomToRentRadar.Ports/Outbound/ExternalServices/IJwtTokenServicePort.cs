using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Ports.Outbound.ExternalServices;

public interface IJwtTokenServicePort
{
    GeneratedAccessToken GenerateAccessToken(User user, DateTime utcNow);
    string GenerateRawRefreshToken();
    string ComputeTokenHash(string rawToken);
    TimeSpan RefreshTokenLifetime { get; }
}

