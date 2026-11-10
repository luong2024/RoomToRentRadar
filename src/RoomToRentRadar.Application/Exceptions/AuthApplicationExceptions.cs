namespace RoomToRentRadar.Application.Exceptions;

public abstract class AuthApplicationException : Exception
{
    protected AuthApplicationException(string message)
        : base(message)
    {
    }

    protected AuthApplicationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class DuplicateEmailException : AuthApplicationException
{
    public DuplicateEmailException()
        : base("An account with this email address already exists.")
    {
    }

    public DuplicateEmailException(Exception innerException)
        : base("An account with this email address already exists.", innerException)
    {
    }
}

public sealed class ExternalIdentityConflictException : AuthApplicationException
{
    public ExternalIdentityConflictException()
        : base("An account with this email address already exists under another sign-in method and cannot be automatically linked.")
    {
    }
}

public sealed class AuthenticationFailedException : AuthApplicationException
{
    public AuthenticationFailedException(string message = "Invalid credentials.")
        : base(message)
    {
    }
}

public sealed class InvalidGoogleTokenException : AuthApplicationException
{
    public InvalidGoogleTokenException(string message = "The Google ID token is invalid or could not be verified.")
        : base(message)
    {
    }

    public InvalidGoogleTokenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class InvalidRefreshTokenException : AuthApplicationException
{
    public InvalidRefreshTokenException(string message = "The refresh token is invalid, expired, or has been revoked.")
        : base(message)
    {
    }
}

public sealed class RequestValidationException : AuthApplicationException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public RequestValidationException(string field, string errorMessage)
        : this(new Dictionary<string, string[]> { [field] = [errorMessage] })
    {
    }
}
