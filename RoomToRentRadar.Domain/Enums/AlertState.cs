namespace RoomToRentRadar.Domain.Enums;

/// <summary>Alert state of a saved search, matching <c>AlertState</c> in <c>docs/api/openapi.yaml</c>.</summary>
public enum AlertState
{
    Off = 1,
    Active = 2,
    Paused = 3
}