namespace RoomToRentRadar.Application.DTOs;

public record SearchRoomsResult(
    IReadOnlyList<RoomSummaryDto> Items,
    int TotalCount,
    int TotalPages,
    int CurrentPage,
    IReadOnlyList<string> ActiveFilters
);
