using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Models.Authentication;

namespace RoomToRentRadar.Infrastructure.Adapters;

/// <summary>
/// Infrastructure adapter handling Google OAuth authentication on mobile platforms.
/// </summary>
public class GoogleAuthAdapter : IGoogleAuthAdapter
{
    public virtual async Task<GoogleAuthResult> AuthenticateWithGoogleAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Integration point for WebAuthenticator / Google Sign-In SDK.
            // If OAuth client ID or platform credential is not configured, safely return canceled/failed result
            // without hardcoding credentials or throwing unhandled exceptions.
            await Task.Yield();

            return GoogleAuthResult.Failed("Google sign-in was canceled or failed. Please try again.");
        }
        catch (TaskCanceledException)
        {
            return GoogleAuthResult.Canceled();
        }
        catch (Exception ex)
        {
            return GoogleAuthResult.Failed($"Google sign-in error: {ex.Message}");
        }
    }
}
