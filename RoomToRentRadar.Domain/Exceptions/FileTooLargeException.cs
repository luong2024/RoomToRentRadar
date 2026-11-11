namespace RoomToRentRadar.Domain.Exceptions;

public sealed class FileTooLargeException : Exception
{
    public FileTooLargeException(long maxBytes)
        : base($"File exceeds the maximum allowed size of {maxBytes} bytes.")
    {
    }
}
