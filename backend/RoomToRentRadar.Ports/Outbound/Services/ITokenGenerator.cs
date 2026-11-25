using RoomToRentRadar.Ports.Models.Auth;

namespace RoomToRentRadar.Ports.Outbound.Services;

public interface ITokenGenerator
{
    (string Token, string TokenHash) Create(AuthTokenSubject subject, DateTime expiresAt);

    string Hash(string token);
}
