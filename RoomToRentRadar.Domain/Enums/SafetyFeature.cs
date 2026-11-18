namespace RoomToRentRadar.Domain.Enums;

/// <summary>Safety features of a room, matching <c>SafetyFeature</c> in <c>docs/api/openapi.yaml</c>.</summary>
public enum SafetyFeature
{
    Cctv = 1,
    SecureEntrance = 2,
    FireSafety = 3,
    OwnerOrGuardOnSite = 4,
    WellLitAccess = 5
}