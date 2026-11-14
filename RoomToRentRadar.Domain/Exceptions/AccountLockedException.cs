namespace RoomToRentRadar.Domain.Exceptions;

public class AccountLockedException : Exception
{
    public DateTime LockoutEndUtc { get; }

    public AccountLockedException(DateTime lockoutEndUtc)
        : base($"Account is temporarily locked until {lockoutEndUtc:O}.")
    {
        LockoutEndUtc = lockoutEndUtc;
    }
}