namespace RoomToRentRadar.Application.Ports.Outbound;

/// <summary>
/// Outbound port for secure password hashing and verification.
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);

    bool VerifyPassword(string password, string passwordHash);
}
