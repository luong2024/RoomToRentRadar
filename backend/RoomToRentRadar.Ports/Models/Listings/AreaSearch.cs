using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Ports.Models.Listings;

/// <summary>
/// The optional distance reference of an area search. The coordinates are nullable
/// so a provided origin object with a missing coordinate is rejected instead of
/// silently falling back to the polygon centroid.
/// </summary>
public sealed record AreaSearchOrigin(double? Lat, double? Lng);

public sealed record AreaSearchQuery(
    ListingQuery Query,
    AreaPolygon? Area,
    AreaSearchOrigin? Origin);

public sealed record AreaSearchError(
    string Code,
    string Detail,
    string? Field = null);

public sealed record AreaSearchResult(
    SearchResultDto<ListingSummaryDto>? Page,
    AreaSearchError? Error)
{
    public bool IsSuccess => Error is null;

    public static AreaSearchResult Success(SearchResultDto<ListingSummaryDto> page) => new(page, null);

    public static AreaSearchResult Failure(string code, string detail, string? field = null)
        => new(null, new AreaSearchError(code, detail, field));
}

public static class AreaSearchErrorCodes
{
    public const string InvalidOrigin = "invalid_origin";
    public const string InvalidCursor = "invalid_cursor";
    public const string CursorSortMismatch = "cursor_sort_mismatch";
    public const string CursorFilterMismatch = "cursor_filter_mismatch";
}
