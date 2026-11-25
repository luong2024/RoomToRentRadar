namespace RoomToRentRadar.Domain.Exceptions;

/// <summary>
/// Base domain exception for listing report invariant violations.
/// </summary>
public class ReportDomainException : DomainException
{
    public ReportDomainException(string message)
        : base(message)
    {
    }
}
