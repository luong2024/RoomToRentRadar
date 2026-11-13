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

public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message = "A concurrency conflict occurred. The resource was modified by another operation.")
        : base(message)
    {
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

public class PhoneNumberRequiredException : Exception
{
    public PhoneNumberRequiredException(string message = "Phone number is required for Landlord registration.")
        : base(message)
    {
    }
}

public class PasswordRequiredException : Exception
{
    public PasswordRequiredException(string message = "Password creation is required for initial Google registration.")
        : base(message)
    {
    }
}

public class CurrentPasswordRequiredException : Exception
{
    public CurrentPasswordRequiredException(string message = "Current password is required to link this Google account.")
        : base(message)
    {
    }
}

public class HouseNameRequiredException : Exception
{
    public HouseNameRequiredException(string message = "House name is required for Landlord registration.")
        : base(message)
    {
    }
}

