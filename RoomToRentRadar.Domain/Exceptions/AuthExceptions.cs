namespace RoomToRentRadar.Domain.Exceptions;

public class DomainValidationException : Exception
{
    public DomainValidationException(string message) : base(message)
    {
    }
}

public class DuplicateEmailException : Exception
{
    public string Email { get; }

    public DuplicateEmailException(string email)
        : base($"An account with email '{email}' is already registered.")
    {
        Email = email;
    }
}

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException(string message = "Invalid email or password.")
        : base(message)
    {
    }
}

public class AccountLockedException : Exception
{
    public DateTime LockoutEndUtc { get; }

    public AccountLockedException(DateTime lockoutEndUtc)
        : base($"Account is temporarily locked until {lockoutEndUtc:O}.")
    {
        LockoutEndUtc = lockoutEndUtc;
    }
}

public class ForbiddenRoleException : Exception
{
    public ForbiddenRoleException(string message) : base(message)
    {
    }
}

public class InvalidTokenException : Exception
{
    public InvalidTokenException(string message = "The provided token is invalid, expired, or has been revoked.")
        : base(message)
    {
    }
}

public class LandlordNotApprovedException : Exception
{
    public string UserId { get; }

    public LandlordNotApprovedException(string userId)
        : base($"Landlord account '{userId}' is pending Admin approval and cannot publish listings yet.")
    {
        UserId = userId;
    }
}

public class UserNotFoundException : Exception
{
    public string UserId { get; }

    public UserNotFoundException(string userId)
        : base($"User with ID '{userId}' was not found.")
    {
        UserId = userId;
    }
}

