namespace RoomToRentRadar.Domain.Exceptions;

public class UserNotFoundException : Exception
{
    public string UserId { get; }

    public UserNotFoundException(string userId)
        : base($"User with ID '{userId}' was not found.")
    {
        UserId = userId;
    }
}