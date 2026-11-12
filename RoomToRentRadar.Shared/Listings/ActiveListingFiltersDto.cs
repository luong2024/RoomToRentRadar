using System.Text.Json.Serialization;

namespace RoomToRentRadar.Shared.Listings;

/// <summary>
/// The filters that were actually applied to a listing search. Every member is
/// omitted from the payload when the client did not send it, so an absent member
/// means "not filtered".
/// </summary>
public sealed record ActiveListingFiltersDto(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? MinPrice,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? MaxPrice,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? MinAreaM2,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? MaxAreaM2,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Bedrooms,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RoomType? RoomType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? CampusLat,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? CampusLng,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? MaxDistanceKm);
