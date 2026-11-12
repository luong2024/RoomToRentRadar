using Microsoft.Maui.Storage;

namespace RoomToRentRadar.DeviceAdapters;

public sealed class GoogleAuthClientService : IGoogleAuthClientService
{
    private readonly WebAuthenticatorGoogleAuthClientService _realService;
    private readonly MockGoogleAuthClientService _mockService;

    public GoogleAuthClientService(
        WebAuthenticatorGoogleAuthClientService? realService = null,
        MockGoogleAuthClientService? mockService = null)
    {
        _realService = realService ?? new WebAuthenticatorGoogleAuthClientService();
        _mockService = mockService ?? new MockGoogleAuthClientService();
    }

    public async Task<string?> AuthenticateAndGetIdTokenAsync(string? roleHint = null, CancellationToken ct = default)
    {
        var useMock = Preferences.Default.Get("UseMockGoogleAuth",
#if DEBUG
            true
#else
            false
#endif
        );

        if (useMock)
        {
            return await _mockService.AuthenticateAndGetIdTokenAsync(roleHint, ct);
        }

        try
        {
            var token = await _realService.AuthenticateAndGetIdTokenAsync(roleHint, ct);
            return token;
        }
        catch (Exception)
        {
#if DEBUG
            return await _mockService.AuthenticateAndGetIdTokenAsync(roleHint, ct);
#else
            throw;
#endif
        }
    }
}

