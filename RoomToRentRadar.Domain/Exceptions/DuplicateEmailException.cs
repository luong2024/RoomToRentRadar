namespace RoomToRentRadar.Domain.Exceptions;

public class DuplicateEmailException : Exception
{
    public string Email { get; }

    public DuplicateEmailException(string email)
        : base($"An account with email '{email}' is already registered.")
    {
        Email = email;
    }
}