namespace RoomToRentRadar.Domain.Exceptions;

/// <summary>Maps to HTTP 404; <c>errorCode</c> is a stable UPPER_SNAKE code such as ROOM_NOT_FOUND (ADR-012).</summary>
public class EntityNotFoundException : Exception
{
    private const string DefaultMessage = "The requested resource was not found.";

    public string ErrorCode { get; }

    public EntityNotFoundException(string errorCode, string? message = null)
        : base(message ?? DefaultMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ErrorCode = errorCode;
    }
}