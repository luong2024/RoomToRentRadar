namespace RoomToRentRadar.Domain.Exceptions;

public class InvalidTokenException : Exception
{
    public InvalidTokenException(string message = "The provided token is invalid, expired, or has been revoked.")
        : base(message)
    {
    }
}