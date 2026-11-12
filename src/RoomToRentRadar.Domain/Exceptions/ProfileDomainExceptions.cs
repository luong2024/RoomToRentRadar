namespace RoomToRentRadar.Domain.Exceptions;

public class ProfileDomainException : DomainException
{
    public ProfileDomainException(string message) : base(message) { }
}

public sealed class ProfileNotFoundException : ProfileDomainException
{
    public ProfileNotFoundException(Guid userId)
        : base($"Profile for user '{userId}' was not found.") { }
}

public sealed class InvalidPhoneNumberException : ProfileDomainException
{
    public InvalidPhoneNumberException(string message) : base(message) { }
}

public sealed class ProfileRoleMismatchException : ProfileDomainException
{
    public ProfileRoleMismatchException(string message) : base(message) { }
}

public sealed class KycOperationInvalidException : ProfileDomainException
{
    public KycOperationInvalidException(string message) : base(message) { }
}