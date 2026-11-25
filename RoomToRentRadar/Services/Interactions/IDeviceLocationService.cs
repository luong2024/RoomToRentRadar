namespace RoomToRentRadar.Services.Interactions;

public enum DeviceLocationStatus
{
    Success,
    LocationDisabled,
    PermissionDenied,
    NotSupported,
    UnableToLocate
}

public sealed record DeviceLocationResult(
    DeviceLocationStatus Status,
    double? Latitude = null,
    double? Longitude = null,
    double? AccuracyMeters = null);

public interface IDeviceLocationService
{
    Task<DeviceLocationResult> GetCurrentLocationAsync(CancellationToken cancellationToken = default);
    Task<bool> OpenLocationSettingsAsync();
}
