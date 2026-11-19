using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Ports.Outbound.ExternalServices;

public interface IIctuSsoVerifierPort
{
    Task<IctuSsoUserPayload> VerifyIdTokenAsync(string idToken, CancellationToken ct = default);
}

