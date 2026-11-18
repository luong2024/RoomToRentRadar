using RoomToRentRadar.Shared.Pagination;

namespace RoomToRentRadar.Shared.Moderation;

public sealed record ModerationListingQueueDto(
    IReadOnlyList<ModerationListingDto> Items,
    PaginationInfo Pagination);
