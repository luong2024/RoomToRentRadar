namespace RoomToRentRadar.Validations;

public static class ProfileFormValidator
{
    public static string NormalizePhone(string? phone)
        => new((phone ?? string.Empty)
            .Where(character => !char.IsWhiteSpace(character) && character is not '-' and not '.')
            .ToArray());

    public static bool IsValidPhone(string? phone)
    {
        var normalized = NormalizePhone(phone);
        return normalized.Length == 10
            && normalized[0] == '0'
            && normalized.All(char.IsAsciiDigit);
    }

    public static bool IsValidBirthDate(DateTime dateOfBirth)
        => dateOfBirth.Date < DateTime.Today;
}
