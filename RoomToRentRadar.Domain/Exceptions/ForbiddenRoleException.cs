namespace RoomToRentRadar.Domain.Exceptions;

public class ForbiddenRoleException : Exception
{
    public ForbiddenRoleException(string message) : base(message)
    {
    }
}