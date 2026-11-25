namespace RoomToRentRadar.Application.Exceptions;

/// <summary>
/// Thrown when a user attempts to submit a duplicate active report for the same listing.
/// </summary>
public sealed class DuplicateReportException : Exception
{
    public DuplicateReportException(string message)
        : base(message)
    {
    }
}
