namespace RoomToRentRadar.Ports.Outbound.Services;

public interface IPasswordHasher
{
    (string Hash, string Salt) Hash(string password);

    bool Verify(string password, string hash, string salt);

    /// <summary>Burns the same KDF cost for unknown accounts so login timing stays uniform.</summary>
    bool VerifyDummy(string password);
}
