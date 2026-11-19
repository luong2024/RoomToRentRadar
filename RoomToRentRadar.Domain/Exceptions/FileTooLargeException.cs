namespace RoomToRentRadar.Domain.Exceptions;

/// <summary>
/// The uploaded file exceeds the 10 MB limit of BR-MED-02. Maps to HTTP 413 with
/// <c>errorCode = FILE_TOO_LARGE</c> (ADR-012, docs/system-architecture.md §7.2).
/// </summary>
public class FileTooLargeException : Exception
{
    private const string DefaultMessage = "The uploaded file exceeds the maximum allowed size of 10 MB.";

    public FileTooLargeException(string? message = null, Exception? innerException = null)
        : base(message ?? DefaultMessage, innerException)
    {
    }
}
