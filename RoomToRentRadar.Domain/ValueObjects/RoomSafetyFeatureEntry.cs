using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Domain.ValueObjects;

/// <summary>
/// One declared safety criterion of a listing with the Admin confirmation flag
/// (<c>docs/erd.md</c> §4.6, BR-LST-08). A landlord edit that adds a feature stores it unconfirmed.
/// </summary>
public sealed record RoomSafetyFeatureEntry(SafetyFeature Feature, bool IsConfirmed);
