using System.Text.Json.Serialization;

namespace RoomToRentRadar.Shared.Pagination;

public sealed record PaginationInfo(
    int Limit,
    [property: JsonPropertyName("next_cursor")] string? NextCursor,
    [property: JsonPropertyName("has_more")] bool HasMore);
