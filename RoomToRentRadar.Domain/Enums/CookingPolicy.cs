namespace RoomToRentRadar.Domain.Enums;

/// <summary>Cooking policy whitelist of house rules (<c>docs/erd.md</c> §4.7, BR-HR-01).</summary>
public enum CookingPolicy
{
    Allowed = 1,
    SharedKitchenOnly = 2,
    NotAllowed = 3
}
