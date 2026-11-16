namespace RoomToRentRadar.Application.DTOs;

/// <summary>
/// Query parameters for fetching rental room listings near a specific campus.
/// </summary>
public sealed record GetRoomListingsQuery(
    string CampusId,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = "distance",
    string? SortOrder = "asc");
