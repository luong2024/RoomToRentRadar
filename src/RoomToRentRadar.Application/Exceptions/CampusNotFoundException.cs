namespace RoomToRentRadar.Application.Exceptions;

/// <summary>
/// Thrown when the specified campus is not found.
/// </summary>
public sealed class CampusNotFoundException : Exception
{
    public CampusNotFoundException(string campusId)
        : base($"Campus with ID '{campusId}' was not found.")
    {
    }
}
