namespace RoomToRentRadar.Ports.Outbound.ExternalServices;

public interface IPasswordHasherPort
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}

