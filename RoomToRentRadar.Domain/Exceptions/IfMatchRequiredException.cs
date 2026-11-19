namespace RoomToRentRadar.Domain.Exceptions;

/// <summary>
/// The client called a mutation without the <c>If-Match</c> header. Maps to HTTP 428 with
/// <c>errorCode = IF_MATCH_REQUIRED</c> (ADR-012, docs/system-architecture.md §7.2).
/// </summary>
public class IfMatchRequiredException : Exception
{
    private const string DefaultMessage = "The If-Match header is required for this operation.";

    public IfMatchRequiredException(string? message = null, Exception? innerException = null)
        : base(message ?? DefaultMessage, innerException)
    {
    }
}
