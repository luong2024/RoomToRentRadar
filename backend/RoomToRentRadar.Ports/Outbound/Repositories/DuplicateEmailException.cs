namespace RoomToRentRadar.Ports.Outbound.Repositories;

/// <summary>Raised by the user store when a concurrent insert loses the unique email index.</summary>
public sealed class DuplicateEmailException : Exception
{
    public DuplicateEmailException(Exception innerException)
        : base("An account with this email address already exists.", innerException)
    {
    }
}
