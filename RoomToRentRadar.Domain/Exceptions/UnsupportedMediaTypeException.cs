namespace RoomToRentRadar.Domain.Exceptions;

public sealed class UnsupportedMediaTypeException : Exception
{
    public UnsupportedMediaTypeException(string message = "Only JPEG and PNG images are supported.")
        : base(message)
    {
    }
}
