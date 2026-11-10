using System.Text.RegularExpressions;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.ValueObjects;

/// <summary>
/// Value Object representing a validated and canonically normalized email address.
/// Policy: trimmed and lower-cased using invariant culture.
/// </summary>
public sealed record EmailAddress
{
    public const int MaxLength = 254;

    private static readonly Regex EmailPattern = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));

    public string Value { get; }

    private EmailAddress(string value)
    {
        Value = value;
    }

    public static EmailAddress Create(string? rawEmail)
    {
        if (string.IsNullOrWhiteSpace(rawEmail))
        {
            throw new InvalidEmailException("Email address is required.");
        }

        var normalized = rawEmail.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength)
        {
            throw new InvalidEmailException($"Email address must not exceed {MaxLength} characters.");
        }

        var atIndex = normalized.IndexOf('@');
        if (atIndex <= 0 || atIndex != normalized.LastIndexOf('@') || atIndex == normalized.Length - 1)
        {
            throw new InvalidEmailException("Email address format is invalid.");
        }

        var localPart = normalized[..atIndex];
        var domainPart = normalized[(atIndex + 1)..];

        if (localPart.Length > 64 ||
            domainPart.StartsWith('.') ||
            domainPart.EndsWith('.') ||
            domainPart.Contains("..", StringComparison.Ordinal) ||
            !EmailPattern.IsMatch(normalized))
        {
            throw new InvalidEmailException("Email address format is invalid.");
        }

        return new EmailAddress(normalized);
    }

    public override string ToString() => Value;
}
