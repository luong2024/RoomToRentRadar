namespace RoomToRentRadar.Domain.Exceptions;

public class ProfileDomainException : DomainException
{
    public ProfileDomainException(string message) : base(message) { }
    public ProfileDomainException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class ProfileNotFoundException : ProfileDomainException
{
    public ProfileNotFoundException(Guid userId)
        : base($"Profile for user '{userId}' was not found.") { }
}

public sealed class InvalidPhoneNumberException : ProfileDomainException
{
    public InvalidPhoneNumberException(string message) : base(message) { }
    public InvalidPhoneNumberException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class ProfileRoleMismatchException : ProfileDomainException
{
    public ProfileRoleMismatchException(string message) : base(message) { }
    public ProfileRoleMismatchException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class KycOperationInvalidException : ProfileDomainException
{
    public KycOperationInvalidException(string message) : base(message) { }
    public KycOperationInvalidException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class ProfileValidationException : ProfileDomainException
{
    public ProfileValidationException(string message) : base(message) { }
    public ProfileValidationException(string message, Exception innerException) : base(message, innerException) { }
}
