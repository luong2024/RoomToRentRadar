namespace RoomToRentRadar.Domain.Exceptions;

public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message = "A concurrency conflict occurred. The resource was modified by another operation.")
        : base(message)
    {
    }
}