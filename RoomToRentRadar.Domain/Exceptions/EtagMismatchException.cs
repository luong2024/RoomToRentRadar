namespace RoomToRentRadar.Domain.Exceptions;

/// <summary>
/// The supplied <c>If-Match</c> value does not match the current resource version. Maps to HTTP 412
/// with <c>errorCode = ETAG_MISMATCH</c> (ADR-012, docs/system-architecture.md §7.2).
/// </summary>
public class EtagMismatchException : Exception
{
    private const string DefaultMessage = "The supplied If-Match value does not match the current resource version.";

    public EtagMismatchException(string? message = null, Exception? innerException = null)
        : base(message ?? DefaultMessage, innerException)
    {
    }
}
