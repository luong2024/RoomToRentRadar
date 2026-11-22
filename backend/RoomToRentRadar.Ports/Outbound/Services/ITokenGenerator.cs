namespace RoomToRentRadar.Ports.Outbound.Services;

public interface ITokenGenerator
{
    (string Token, string TokenHash) Create();

    string Hash(string token);
}
