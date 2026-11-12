namespace RoomToRentRadar.Ports.Contracts.Responses.Rooms;

/// <summary>
/// Photo rendition metadata per docs/api/openapi.yaml schema PhotoDto and BR-MED-04.
/// </summary>
public sealed record PhotoDto(
    string Id,
    string Kind,
    string ThumbUrl,
    string MediumUrl,
    string OriginalUrl,
    int SortOrder,
    int? Width = null,
    int? Height = null
);
