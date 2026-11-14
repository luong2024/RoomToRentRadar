namespace RoomToRentRadar.Domain.Exceptions;

public class DomainValidationException : Exception
{
    /// <summary>Stable UPPER_SNAKE code exposed as <c>errorCode</c> in ProblemDetails (ADR-012).</summary>
    public string? ErrorCode { get; }

    public DomainValidationException(string message, string? errorCode = null) : base(message)
    {
        ErrorCode = errorCode;
    }
}