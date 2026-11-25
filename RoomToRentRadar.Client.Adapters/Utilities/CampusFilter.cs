using System.Globalization;
using System.Text;
using RoomToRentRadar.Ports.Contracts.Responses.Campuses;

namespace RoomToRentRadar.Client.Adapters.Utilities;

/// <summary>Campus search for SCR-ACC-02: matches code or name, ignoring case and Vietnamese diacritics.</summary>
public static class CampusFilter
{
    public static IReadOnlyList<CampusDto> Filter(IReadOnlyList<CampusDto> all, string? query)
    {
        ArgumentNullException.ThrowIfNull(all);

        var needle = Normalize(query);
        if (needle.Length == 0)
        {
            return all;
        }

        return all
            .Where(c => Normalize(c.ShortName).Contains(needle, StringComparison.Ordinal)
                || Normalize(c.Id).Contains(needle, StringComparison.Ordinal)
                || Normalize(c.Name).Contains(needle, StringComparison.Ordinal))
            .ToList();
    }

    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            // "đ" has no canonical decomposition.
            builder.Append(character == 'đ' ? 'd' : character);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
