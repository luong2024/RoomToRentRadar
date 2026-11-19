namespace RoomToRentRadar.Ports.Contracts.Responses.Notifications;

using RoomToRentRadar.Ports.Contracts.Common;

/// <summary>Matches <c>PagedNotifications</c> in <c>docs/api/openapi.yaml</c> (PageMeta + items + unreadCount).</summary>
public sealed record PagedNotifications(
    IReadOnlyList<NotificationDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int UnreadCount
) : PagedResult<NotificationDto>(Items, TotalCount, PageNumber, PageSize);
