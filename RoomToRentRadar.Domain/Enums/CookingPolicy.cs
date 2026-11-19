namespace RoomToRentRadar.Domain.Enums;

/// <summary>Cooking rules of a room, matching <c>CookingPolicy</c> in <c>docs/api/openapi.yaml</c>.</summary>
public enum CookingPolicy
{
    Allowed = 1,
    SharedKitchenOnly = 2,
    NotAllowed = 3
}
