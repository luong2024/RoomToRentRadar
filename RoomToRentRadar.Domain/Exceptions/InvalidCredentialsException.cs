namespace RoomToRentRadar.Domain.Exceptions;

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException(string message = "Invalid email or password.")
        : base(message)
    {
    }
}