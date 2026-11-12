using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadar.Ports.Outbound.ExternalServices;

public interface IGoogleAuthVerifierPort
{
    Task<GoogleUserPayload> VerifyIdTokenAsync(string idToken, CancellationToken ct = default);
}
