using System.Globalization;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Application.UseCases.Landlord;

/// <summary>
/// Parses the <c>If-Match</c> header of the landlord mutations (ADR-012 §5). A missing header is
/// <c>428 IF_MATCH_REQUIRED</c>; anything that is not a positive integer version (optionally wrapped
/// in quotes or a weak <c>W/</c> prefix) is <c>412 ETAG_MISMATCH</c>.
/// </summary>
internal static class IfMatchParser
{
    public static int ParseRequiredVersion(string? ifMatch)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            throw new IfMatchRequiredException();
        }

        var value = ifMatch.Trim();
        if (value.StartsWith("W/", StringComparison.Ordinal))
        {
            value = value[2..].Trim();
        }

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
        }

        // NumberStyles.None rejects signs, whitespace and thousands separators, so only digits parse.
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version < 1)
        {
            throw new EtagMismatchException();
        }

        return version;
    }
}
