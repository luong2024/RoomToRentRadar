using Microsoft.Extensions.Logging;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Ports.Contracts.Requests.Notifications;
using RoomToRentRadar.Ports.Contracts.Responses.Notifications;

namespace RoomToRentRadar.Client.Adapters.Http;

/// <summary>Notification endpoints arrive with T07.2; every operation is a task-id stub until then (PLAN §5.4).</summary>
public sealed class HttpNotificationGateway : INotificationGateway
{
    public HttpNotificationGateway(HttpClient httpClient, ILogger<HttpNotificationGateway>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
    }

    public Task RegisterDeviceAsync(Guid installationId, RegisterDeviceRequest request, CancellationToken ct = default) =>
        Pending.For("T07.2", "registerDevice");

    public Task UnregisterDeviceAsync(Guid installationId, CancellationToken ct = default) =>
        Pending.For("T07.2", "unregisterDevice");

    public Task<PagedNotifications> ListMyNotificationsAsync(int pageNumber = 1, int pageSize = 20, CancellationToken ct = default) =>
        Pending.For<PagedNotifications>("T07.2", "listMyNotifications");

    public Task MarkNotificationsReadAsync(MarkNotificationsReadRequest request, CancellationToken ct = default) =>
        Pending.For("T07.2", "markNotificationsRead");
}