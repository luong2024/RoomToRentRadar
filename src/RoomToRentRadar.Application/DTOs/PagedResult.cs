namespace RoomToRentRadar.Application.DTOs;

/// <summary>
/// Standard paginated result wrapper for application queries.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
