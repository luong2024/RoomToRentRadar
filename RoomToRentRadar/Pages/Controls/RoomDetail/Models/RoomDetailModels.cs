using Fonts;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Pages.Controls.RoomDetail.Models;

/// <summary>
/// Display model for a single key fact card (Area, Room type, Occupants, Furnishing, Availability, Distance).
/// </summary>
public sealed record KeyFactItem(string IconGlyph, string Label, string Value);

/// <summary>
/// Display model for a line item in the monthly utility costs breakdown.
/// </summary>
public sealed record CostRowItem(string Label, string ValueFormatted, bool IsHighlighted = false);

/// <summary>
/// Display model for an amenity badge or grid item.
/// </summary>
public sealed record AmenityDisplayItem(Amenity Amenity, string Name, string IconGlyph);

/// <summary>
/// Display model for a safety verification item (CCTV, fire extinguisher, secure entrance, etc.).
/// </summary>
public sealed record SafetyCheckItem(SafetyFeature Feature, string Description, bool IsVerified, string IconGlyph)
{
    public string IconColor => IsVerified ? "#16A34A" : "#94A3B8";
}

/// <summary>
/// Display model for a house rule entry (quiet hours, curfew, guests, cooking, parking).
/// </summary>
public sealed record HouseRuleItem(string IconGlyph, string Title, string Detail);

/// <summary>
/// Display model for a commute mode estimate (motorbike, bus, walking).
/// </summary>
public sealed record CommuteModeItem(TravelMode Mode, string IconGlyph, string TimeFormatted, string Label);

/// <summary>
/// Lightweight card model for similar rooms shown when target listing is unavailable.
/// </summary>
public sealed record SimilarRoomDisplayModel(
    string Id,
    string Title,
    string PriceFormatted,
    string DistanceFormatted,
    string? ThumbnailUrl);
