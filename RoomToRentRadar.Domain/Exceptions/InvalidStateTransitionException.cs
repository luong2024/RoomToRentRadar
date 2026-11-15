namespace RoomToRentRadar.Domain.Exceptions;

/// <summary>ADR-011: illegal listing state transition - mapped to HTTP 409.</summary>
public class InvalidStateTransitionException : Exception
{
    public string? ErrorCode { get; }

    public InvalidStateTransitionException(string message, string? errorCode = null) : base(message)
    {
        ErrorCode = errorCode;
    }
}
