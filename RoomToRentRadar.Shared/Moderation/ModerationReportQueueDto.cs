using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadar.Shared.Moderation;

public sealed record ModerationReportQueueDto(
    IReadOnlyList<ModerationReportGroupDto> Items,
    PaginationInfo Pagination);
