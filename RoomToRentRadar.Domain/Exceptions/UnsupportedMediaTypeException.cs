namespace RoomToRentRadar.Domain.Exceptions;

/// <summary>
/// The uploaded file is not a real JPEG or PNG (magic-byte check, BR-MED-02). Maps to HTTP 415 with
/// <c>errorCode = UNSUPPORTED_MEDIA_TYPE</c> (ADR-012, docs/system-architecture.md §7.2).
/// </summary>
public class UnsupportedMediaTypeException : Exception
{
    private const string DefaultMessage = "Only JPEG and PNG images are supported.";

    public UnsupportedMediaTypeException(string? message = null, Exception? innerException = null)
        : base(message ?? DefaultMessage, innerException)
    {
    }
}
