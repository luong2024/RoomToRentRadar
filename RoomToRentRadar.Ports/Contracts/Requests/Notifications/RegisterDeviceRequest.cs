namespace RoomToRentRadar.Ports.Contracts.Requests.Notifications;

using RoomToRentRadar.Domain.Enums;

/// <summary>Matches <c>RegisterDeviceRequest</c> in <c>docs/api/openapi.yaml</c> 1:1.</summary>
public sealed record RegisterDeviceRequest(
    string PushToken,
    DevicePlatform Platform
);
