namespace RoomToRentRadar.Domain.Exceptions;

public sealed class InvalidEmailException : DomainException
{
    public InvalidEmailException(string message)
        : base(message)
    {
    }
}

public sealed class InvalidRoleException : DomainException
{
    public InvalidRoleException(string message)
        : base(message)
    {
    }
}

public sealed class AdminSelfRegistrationForbiddenException : DomainException
{
    public AdminSelfRegistrationForbiddenException()
        : base("Self-registration with the ADMIN role is forbidden.")
    {
    }
}

public sealed class UserInactiveException : DomainException
{
    public UserInactiveException()
        : base("The user account is inactive.")
    {
    }
}

public sealed class InvalidUserStateException : DomainException
{
    public InvalidUserStateException(string message)
        : base(message)
    {
    }
}

public sealed class InvalidRefreshTokenStateException : DomainException
{
    public InvalidRefreshTokenStateException(string message)
        : base(message)
    {
    }
}
