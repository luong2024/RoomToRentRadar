using RoomToRentRadar.Ports.Contracts.Requests.Notifications;
using RoomToRentRadar.Ports.Contracts.Responses.Notifications;

namespace RoomToRentRadar.Client.Adapters.Abstractions;

/// <summary>Push devices and notification inbox (Notifications tag of <c>docs/api/openapi.yaml</c>, BR-ALR-06).</summary>
public interface INotificationGateway
{
    [ApiOperation("registerDevice")]
    Task RegisterDeviceAsync(Guid installationId, RegisterDeviceRequest request, CancellationToken ct = default);

    [ApiOperation("unregisterDevice")]
    Task UnregisterDeviceAsync(Guid installationId, CancellationToken ct = default);

    [ApiOperation("listMyNotifications")]
    Task<PagedNotifications> ListMyNotificationsAsync(int pageNumber = 1, int pageSize = 20, CancellationToken ct = default);

    [ApiOperation("markNotificationsRead")]
    Task MarkNotificationsReadAsync(MarkNotificationsReadRequest request, CancellationToken ct = default);
}