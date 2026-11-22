using RoomToRentRadar.Ports.Models.Moderation;
using RoomToRentRadar.Shared.Moderation;

namespace RoomToRentRadar.Ports.Inbound;

public interface IModerationQueueUseCase
{
    Task<ModerationReportQueueDto> GetQueueAsync(
        ModerationQueueQuery query,
        ModerationQueueCursor? cursor,
        CancellationToken cancellationToken);

    Task<ModerationListingQueueDto> GetListingQueueAsync(
        ModerationListingQueueQuery query,
        ModerationListingQueueCursor? cursor,
        CancellationToken cancellationToken);

    Task<ModerationReportDetailDto?> GetReportAsync(
        Guid reportId,
        CancellationToken cancellationToken);
}
