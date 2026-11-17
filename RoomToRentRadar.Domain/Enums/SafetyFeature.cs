namespace RoomToRentRadar.Domain.Enums;

/// <summary>The five safety criteria of the Safety Score (<c>docs/erd.md</c> §4.6, BR-LST-08).</summary>
public enum SafetyFeature
{
    Cctv = 1,
    SecureEntrance = 2,
    FireSafety = 3,
    OwnerOrGuardOnSite = 4,
    WellLitAccess = 5
}
