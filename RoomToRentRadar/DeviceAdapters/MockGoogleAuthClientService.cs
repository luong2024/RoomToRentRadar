namespace RoomToRentRadar.DeviceAdapters;

public sealed class MockGoogleAuthClientService : IGoogleAuthClientService
{
    public Task<string?> AuthenticateAndGetIdTokenAsync(string? roleHint = null, CancellationToken ct = default)
    {
        var token = string.Equals(roleHint, "Landlord", StringComparison.OrdinalIgnoreCase)
            ? "mock-google-landlord-token"
            : "mock-google-student-token";

        return Task.FromResult<string?>(token);
    }
}

