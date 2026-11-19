namespace RoomToRentRadar.Domain.Enums;

/// <summary>Guest rules of a room, matching <c>GuestPolicy</c> in <c>docs/api/openapi.yaml</c>.</summary>
public enum GuestPolicy
{
    Allowed = 1,
    DaytimeOnly = 2,
    NotAllowed = 3
}
