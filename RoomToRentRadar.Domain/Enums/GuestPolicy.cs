namespace RoomToRentRadar.Domain.Enums;

/// <summary>Guest policy whitelist of house rules (<c>docs/erd.md</c> §4.7, BR-HR-01).</summary>
public enum GuestPolicy
{
    Allowed = 1,
    DaytimeOnly = 2,
    NotAllowed = 3
}
