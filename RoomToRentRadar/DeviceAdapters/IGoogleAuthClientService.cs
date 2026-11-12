namespace RoomToRentRadar.DeviceAdapters;

public interface IGoogleAuthClientService
{
    Task<string?> AuthenticateAndGetIdTokenAsync(string? roleHint = null, CancellationToken ct = default);
}

