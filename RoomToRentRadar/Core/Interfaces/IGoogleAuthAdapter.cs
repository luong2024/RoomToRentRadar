using RoomToRentRadar.Core.Models.Authentication;

namespace RoomToRentRadar.Core.Interfaces;

/// <summary>
/// Secondary Adapter Port for interacting with Google Identity provider.
/// </summary>
public interface IGoogleAuthAdapter
{
    Task<GoogleAuthResult> AuthenticateWithGoogleAsync(CancellationToken cancellationToken = default);
}
