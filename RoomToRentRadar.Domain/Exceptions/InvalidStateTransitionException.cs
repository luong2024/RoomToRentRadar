namespace RoomToRentRadar.Domain.Exceptions;

/// <summary>
/// Thrown when an illegal state transition is attempted on a listing per ADR-011 and BR-LST-04.
/// Maps to HTTP 409 Conflict.
/// </summary>
public class InvalidStateTransitionException : Exception
{
    public InvalidStateTransitionException(string message) : base(message)
    {
    }
}
