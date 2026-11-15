namespace RoomToRentRadar.Domain.Exceptions;

/// <summary>Maps to HTTP 409 (ADR-012). A null <see cref="ErrorCode"/> means the API layer uses LIMIT_EXCEEDED.</summary>
public class LimitExceededException : Exception
{
    public string? ErrorCode { get; }

    public LimitExceededException(string message, string? errorCode = null) : base(message)
    {
        ErrorCode = errorCode;
    }
}