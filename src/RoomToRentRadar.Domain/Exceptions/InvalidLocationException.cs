namespace RoomToRentRadar.Domain.Exceptions;

/// <summary>
/// Thrown when geographical coordinates are invalid.
/// </summary>
public sealed class InvalidLocationException : DomainException
{
    public InvalidLocationException()
        : base("Latitude and longitude must be supplied together.")
    {
    }

    public InvalidLocationException(double latitude, double longitude)
        : base($"Invalid coordinates: Latitude ({latitude}) must be between -90 and 90, Longitude ({longitude}) must be between -180 and 180.")
    {
    }
}
