namespace RoomToRentRadar.Domain.Exceptions;

/// <summary>
/// Thrown when a geographic polygon fails domain validation rules.
/// </summary>
public sealed class InvalidPolygonException : DomainException
{
    public InvalidPolygonException(string message)
        : base(message)
    {
    }
}
