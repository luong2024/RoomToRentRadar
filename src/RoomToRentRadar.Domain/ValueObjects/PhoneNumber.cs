using System.Text.RegularExpressions;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.ValueObjects;

public sealed partial record PhoneNumber
{
    private static readonly Regex VietnamPhoneRegex = new(@"^(03|05|07|08|09)\d{8}$", RegexOptions.Compiled);

    public string Value { get; }

    private PhoneNumber(string value) => Value = value;

    public static PhoneNumber Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new InvalidPhoneNumberException("Phone number cannot be empty.");

        var cleaned = input.Trim().Replace(" ", "").Replace("-", "").Replace("+84", "0");
        if (!VietnamPhoneRegex.IsMatch(cleaned))
            throw new InvalidPhoneNumberException($"Phone number '{input}' is not a valid Vietnamese mobile number.");

        return new PhoneNumber(cleaned);
    }

    public static PhoneNumber? CreateOptional(string? input) =>
        string.IsNullOrWhiteSpace(input) ? null : Create(input);

    public override string ToString() => Value;
}