using System.Text.RegularExpressions;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.ValueObjects;

public sealed record EmailAddress
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public const string IctuDomain = "@ictu.edu.vn";

    public string Value { get; }

    private EmailAddress(string value)
    {
        Value = value;
    }

    public static EmailAddress Create(string rawEmail)
    {
        if (string.IsNullOrWhiteSpace(rawEmail))
        {
            throw new DomainValidationException("Email address is required.");
        }

        var normalized = rawEmail.Trim().ToLowerInvariant();
        if (!EmailRegex.IsMatch(normalized))
        {
            throw new DomainValidationException($"'{rawEmail}' is not a valid email address format.");
        }

        return new EmailAddress(normalized);
    }

    public bool IsIctuStudentEmail() =>
        Value.EndsWith(IctuDomain, StringComparison.OrdinalIgnoreCase);

    public override string ToString() => Value;
}
