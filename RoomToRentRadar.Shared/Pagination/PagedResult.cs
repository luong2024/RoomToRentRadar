namespace RoomToRentRadar.Shared.Pagination;

public sealed record PagedResult<T>(IReadOnlyList<T> Data, PaginationInfo Pagination);
